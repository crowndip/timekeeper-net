using ParentalControl.Client.Windows.Services;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Windows;

public class ParentalControlWorker : BackgroundService
{
    private readonly ILogger<ParentalControlWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServerSyncService _syncService;
    private readonly ILocalCache _cache;
    private readonly ISessionMonitor _sessionMonitor;
    private readonly IEnforcementEngine _enforcement;
    private Guid _currentSessionId = Guid.NewGuid();
    private bool _isLocked = false;
    private readonly HashSet<int> _warningsShown = new();
    private int _lastTimeRemaining = int.MaxValue;
    private bool _registered;
    private readonly HashSet<string> _ignoredAccounts = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE", "Administrator"
    };

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
        ILogger<ParentalControlWorker> logger,
        IConfiguration configuration,
        IServerSyncService syncService,
        ILocalCache cache,
        ISessionMonitor sessionMonitor,
        IEnforcementEngine enforcement)
    {
        _logger = logger;
        _configuration = configuration;
        _syncService = syncService;
        _cache = cache;
        _sessionMonitor = sessionMonitor;
        _enforcement = enforcement;
        
        // Subscribe to session lock/unlock events
        _sessionMonitor.SessionChanged += OnSessionChanged;
    }
    
    private void OnSessionChanged(object? sender, SessionChangeEventArgs e)
    {
        switch (e.ChangeType)
        {
            case SessionChangeType.Lock:
                _isLocked = true;
                _logger.LogInformation("Session locked for user {User}", e.Username);
                break;
            case SessionChangeType.Unlock:
                _isLocked = false;
                _logger.LogInformation("Session unlocked for user {User}", e.Username);
                break;
            case SessionChangeType.Logoff:
                _isLocked = false;
                _currentSessionId = Guid.NewGuid(); // New session on next logon
                _logger.LogInformation("User {User} logged off", e.Username);
                break;
            case SessionChangeType.Logon:
                _isLocked = false;
                _logger.LogInformation("User {User} logged on", e.Username);
                break;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parental Control Windows Client starting");

        // Load persisted cache before doing anything else, so a reboot while offline
        // doesn't lose pending usage or cached limits (see LocalCache).
        await _cache.InitializeAsync();

        var tickInterval = _configuration.GetValue<int>("ParentalControl:TickIntervalSeconds", 60);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tick processing");
            }

            await Task.Delay(TimeSpan.FromSeconds(tickInterval), stoppingToken);
        }
    }

    private async Task ProcessTickAsync()
    {
        // Registration/config discovery retries every tick until it succeeds, rather than
        // only once at startup: a computer that boots before the network or server config
        // is ready must still register automatically once it becomes reachable, with no
        // manual intervention. RegisterComputerAsync is idempotent (safe to call repeatedly).
        if (!_registered)
        {
            _registered = await _syncService.RegisterComputerAsync();
            if (_registered)
            {
                var allUsers = await _sessionMonitor.GetAllLocalUsersAsync();
                await _syncService.SyncAllUsersAsync(allUsers);
            }
        }

        var username = _sessionMonitor.GetCurrentUser();
        if (string.IsNullOrEmpty(username) || _ignoredAccounts.Contains(username))
            return;

        // Record time for current user only
        if (_isLocked)
            await _cache.IncrementUsageAsync(Guid.Empty, username, _currentSessionId, 0, 1);
        else
            await _cache.IncrementUsageAsync(Guid.Empty, username, _currentSessionId, 1, 0);

        // Only submit records belonging to the current user to avoid cross-user enforcement
        var allPending = await _cache.GetPendingRecordsAsync();
        var userPending = allPending.Where(r => r.Username == username).ToList();

        if (userPending.Count > 0)
        {
            var response = await _syncService.SubmitUsageAsync(userPending);
            if (response != null)
            {
                _consecutiveSyncFailures = 0;
                await _cache.MarkAsSyncedAsync(userPending.Select(r => r.Id).ToList());
                await CheckEnforcementAsync(username, response);
            }
            else
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

                // Offline: recalculate remaining time from cached limits minus usage since
                // the last successful sync. `userPending` here is exactly that -- every
                // record not yet successfully submitted, which is also exactly when
                // cachedLimits.TimeRemainingMinutes was captured (SubmitUsageAsync only
                // caches limits on success, and only synced records are ever removed).
                // Using the whole day's usage instead double-subtracts minutes the server
                // already accounted for, logging the child out far earlier than their
                // limit actually allows.
                var cachedLimits = await _cache.GetLastKnownLimitsAsync(username);
                if (cachedLimits != null)
                {
                    var minutesSinceLastSync = userPending.Sum(r => r.MinutesActive);
                    var adjustedRemaining = cachedLimits.TimeRemainingMinutes - minutesSinceLastSync;

                    _logger.LogWarning("Offline mode: {Username} has {Remaining} minutes remaining (cached)",
                        username, adjustedRemaining);

                    if (adjustedRemaining <= 0)
                    {
                        _logger.LogWarning("Offline enforcement: time limit reached for {Username}", username);
                        switch (cachedLimits.EnforcementAction)
                        {
                            case "lock":
                                _enforcement.LockSession();
                                break;
                            default:
                                _enforcement.LogoffUser();
                                break;
                        }
                    }
                    else
                    {
                        var adjustedResponse = cachedLimits with
                        {
                            TimeRemainingMinutes = adjustedRemaining,
                            ShouldEnforce = false
                        };
                        await CheckEnforcementAsync(username, adjustedResponse);
                    }
                }
            }
        }
        else
        {
            // No pending usage but user is logged in - check server for time limits
            var response = await _syncService.CheckTimeRemainingAsync(username);
            if (response != null)
                await CheckEnforcementAsync(username, response);
        }
    }

    private async Task CheckEnforcementAsync(string username, UsageReportResponse limits)
    {
        if (limits.ShouldEnforce)
        {
            _logger.LogWarning("Enforcement required for {Username}: {Action}", username, limits.EnforcementAction);
            switch (limits.EnforcementAction) // Bug #3: respect configured action
            {
                case "lock":
                    _enforcement.LockSession();
                    break;
                case "logout":
                default:
                    _enforcement.LogoffUser();
                    break;
            }
            return;
        }

        // Reset warnings when time increases (new day or parent added time) (Bug #7)
        if (limits.TimeRemainingMinutes > _lastTimeRemaining)
        {
            _logger.LogInformation("Time increased from {Old} to {New} minutes, resetting warnings",
                _lastTimeRemaining, limits.TimeRemainingMinutes);
            _warningsShown.Clear();
        }
        _lastTimeRemaining = limits.TimeRemainingMinutes;

        // Use server-configured warning thresholds instead of hardcoded 5 min (Bug #7)
        foreach (var warningMinutes in limits.WarningMinutes)
        {
            if (limits.TimeRemainingMinutes <= warningMinutes && !_warningsShown.Contains(warningMinutes))
            {
                _warningsShown.Add(warningMinutes);
                await _enforcement.ShowWarningAsync(TimeSpan.FromMinutes(limits.TimeRemainingMinutes));
            }
        }
    }
}
