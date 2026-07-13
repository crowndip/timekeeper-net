using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParentalControl.Client.Services;

namespace ParentalControl.Client;

public class ParentalControlWorker : BackgroundService
{
    private readonly ISessionMonitor _sessionMonitor;
    private readonly ITimeTracker _timeTracker;
    private readonly IServerSyncService _serverSync;
    private readonly IEnforcementEngine _enforcement;
    private readonly ILocalCache _cache;
    private readonly ILogger<ParentalControlWorker> _logger;
    private readonly int _tickIntervalSeconds;
    private readonly HashSet<string> _seenSessionIds = new();
    private bool _registered;

    // If the server's database is restored from backup (or otherwise loses its Computer
    // rows) while this client keeps its own persisted ComputerId, every usage submission
    // fails a foreign-key check server-side and returns null forever -- normal offline
    // handling never resolves this because the server IS reachable, it just doesn't
    // recognize this ComputerId anymore. After enough consecutive failures, force
    // re-registration (idempotent and safe even if the real cause is just "still
    // offline" -- one extra failed HTTP call per tick is cheap) so the client
    // self-heals instead of needing a manual service restart.
    private const int ConsecutiveSyncFailuresBeforeReregister = 10;
    private int _consecutiveSyncFailures;

    public ParentalControlWorker(
        ISessionMonitor sessionMonitor,
        ITimeTracker timeTracker,
        IServerSyncService serverSync,
        IEnforcementEngine enforcement,
        ILocalCache cache,
        ILogger<ParentalControlWorker> logger,
        IConfiguration configuration)
    {
        _sessionMonitor = sessionMonitor;
        _timeTracker = timeTracker;
        _serverSync = serverSync;
        _enforcement = enforcement;
        _cache = cache;
        _logger = logger;
        _tickIntervalSeconds = configuration.GetValue<int>("ParentalControl:TickIntervalSeconds", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parental Control Client started, tick interval: {Interval}s", _tickIntervalSeconds);

        // Load persisted cache before doing anything else, so a reboot while offline
        // doesn't lose pending usage or cached limits (see LocalCache).
        await _cache.InitializeAsync();

        // Wait for system to stabilize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tick");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_tickIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Parental Control Client stopping");
    }

    private async Task ProcessTickAsync()
    {
        // Registration/config discovery retries every tick until it succeeds, rather than
        // only once at startup: a computer that boots before the network or server config
        // is ready must still register automatically once it becomes reachable, with no
        // manual intervention. RegisterComputerAsync is idempotent (safe to call repeatedly).
        if (!_registered)
        {
            _registered = await _serverSync.RegisterComputerAsync();
            if (_registered)
            {
                var allUsers = await _sessionMonitor.GetAllLocalUsersAsync();
                await _serverSync.SyncAllUsersAsync(allUsers);
            }
        }

        var sessions = await _sessionMonitor.GetActiveSessionsAsync();

        // Drop tracking state for sessions/users no longer present, so a daemon that runs
        // for months doesn't slowly accumulate an entry per session ID or username ever seen.
        _seenSessionIds.IntersectWith(sessions.Select(s => s.SessionId));
        _enforcement.PruneStaleUsers(sessions.Select(s => s.Username));

        // Check new sessions immediately before recording any time.
        // Prevents ~1 minute of free usage when a child re-logs in after being enforced out.
        foreach (var session in sessions.Where(s => _seenSessionIds.Add(s.SessionId)))
        {
            _logger.LogInformation("New session detected for {Username} ({SessionId}), checking enforcement immediately", session.Username, session.SessionId);
            var response = await _serverSync.CheckTimeRemainingAsync(session.Username);
            if (response != null)
                await _enforcement.CheckAndEnforceAsync(response, session.Username, session.SessionId);
        }

        // Record time for active sessions (deduplicate by username to avoid double-counting)
        var uniqueSessions = sessions
            .GroupBy(s => s.Username)
            .Select(g => g.First())
            .ToList();
            
        foreach (var session in uniqueSessions)
        {
            await _timeTracker.RecordMinuteAsync(session);
        }

        // Always sync with server (even if no active sessions)
        // This allows detecting time adjustments when user is logged off
        var usageData = await _timeTracker.GetPendingUsageAsync();

        if (usageData.Count > 0)
        {
            // Build a lookup of username -> sessionId from active sessions.
            // A user can have multiple sessions (e.g. graphical + TTY); take the first one.
            var sessionMap = sessions
                .GroupBy(s => s.Username)
                .ToDictionary(g => g.Key, g => g.First().SessionId);

            // Enforce per-user so each user gets the correct enforcement action
            var syncedIds = new List<Guid>();
            var anySubmitFailure = false;
            foreach (var userGroup in usageData.GroupBy(r => r.Username))
            {
                var username = userGroup.Key;
                var userRecords = userGroup.ToList();
                var sessionId = sessionMap.TryGetValue(username, out var sid) ? sid : string.Empty;

                // Submit all usage records for this user
                var response = await _serverSync.SubmitUsageAsync(userRecords);
                if (response != null)
                {
                    // Only enforce after ALL records are submitted (response contains final state)
                    await _enforcement.CheckAndEnforceAsync(response, username, sessionId);
                    syncedIds.AddRange(userRecords.Select(u => u.Id));
                }
                else
                {
                    anySubmitFailure = true;
                    _logger.LogWarning("Server unavailable for {Username}, using offline mode", username);
                    await _enforcement.CheckAndEnforceOfflineAsync(userRecords, username, sessionId);
                }
            }

            if (anySubmitFailure)
            {
                _consecutiveSyncFailures++;
                if (_consecutiveSyncFailures >= ConsecutiveSyncFailuresBeforeReregister)
                {
                    _logger.LogWarning(
                        "{Count} consecutive usage-submission failures; forcing re-registration in case the server's database was reset",
                        _consecutiveSyncFailures);
                    _registered = false;
                    _consecutiveSyncFailures = 0;
                }
            }
            else
            {
                _consecutiveSyncFailures = 0;
            }

            if (syncedIds.Count > 0)
                await _timeTracker.MarkAsSyncedAsync(syncedIds);
        }
        else if (sessions.Count > 0)
        {
            // No pending usage but have active sessions - check server for time limits
            // This handles the case where parent added time while child was logged off
            _logger.LogDebug("No pending usage, checking server for {Count} active sessions", sessions.Count);

            // Deduplicate by username - only check once per user even if multiple sessions exist
            var uniqueUsers = sessions
                .GroupBy(s => s.Username)
                .Select(g => g.First())
                .ToList();

            foreach (var session in uniqueUsers)
            {
                var response = await _serverSync.CheckTimeRemainingAsync(session.Username);
                if (response != null)
                {
                    await _enforcement.CheckAndEnforceAsync(response, session.Username, session.SessionId);
                }
            }
        }
    }
}
