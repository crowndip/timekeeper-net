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
    private readonly ILogger<ParentalControlWorker> _logger;
    private readonly int _tickIntervalSeconds;
    
    public ParentalControlWorker(
        ISessionMonitor sessionMonitor,
        ITimeTracker timeTracker,
        IServerSyncService serverSync,
        IEnforcementEngine enforcement,
        ILogger<ParentalControlWorker> logger,
        IConfiguration configuration)
    {
        _sessionMonitor = sessionMonitor;
        _timeTracker = timeTracker;
        _serverSync = serverSync;
        _enforcement = enforcement;
        _logger = logger;
        _tickIntervalSeconds = configuration.GetValue<int>("ParentalControl:TickIntervalSeconds", 60);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parental Control Client started, tick interval: {Interval}s", _tickIntervalSeconds);
        
        // Wait for system to stabilize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        
        // Register with server on startup
        await _serverSync.RegisterComputerAsync();
        
        // Sync all local users with server
        var allUsers = await _sessionMonitor.GetAllLocalUsersAsync();
        await _serverSync.SyncAllUsersAsync(allUsers);
        
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
        var sessions = await _sessionMonitor.GetActiveSessionsAsync();

        // Record time for active sessions
        foreach (var session in sessions)
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
            foreach (var userGroup in usageData.GroupBy(r => r.Username))
            {
                var username = userGroup.Key;
                var userRecords = userGroup.ToList();
                var sessionId = sessionMap.TryGetValue(username, out var sid) ? sid : string.Empty;

                var response = await _serverSync.SubmitUsageAsync(userRecords);
                if (response != null)
                {
                    await _enforcement.CheckAndEnforceAsync(response, username, sessionId);
                    syncedIds.AddRange(userRecords.Select(u => u.Id));
                }
                else
                {
                    _logger.LogWarning("Server unavailable for {Username}, using offline mode", username);
                    await _enforcement.CheckAndEnforceOfflineAsync(userRecords, username, sessionId);
                }
            }

            if (syncedIds.Count > 0)
                await _timeTracker.MarkAsSyncedAsync(syncedIds);
        }
        else if (sessions.Count > 0)
        {
            // No pending usage but have active sessions - check server for time limits
            // This handles the case where parent added time while child was logged off
            _logger.LogDebug("No pending usage, checking server for {Count} active sessions", sessions.Count);

            foreach (var session in sessions)
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
