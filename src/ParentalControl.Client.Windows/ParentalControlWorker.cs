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
    private readonly HashSet<string> _ignoredAccounts = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE", "Administrator"
    };

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

        await _syncService.RegisterComputerAsync();
        
        // Sync all local users with server
        var allUsers = await _sessionMonitor.GetAllLocalUsersAsync();
        await _syncService.SyncAllUsersAsync(allUsers);

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
        var username = _sessionMonitor.GetCurrentUser();
        if (string.IsNullOrEmpty(username) || _ignoredAccounts.Contains(username))
        {
            return;
        }

        // Server will map username to userId automatically
        var userId = Guid.Empty; // Placeholder, server determines actual userId
        
        // Only count time if session is not locked
        // If locked (screen locked or lid closed), record as idle time
        if (_isLocked)
        {
            await _cache.IncrementUsageAsync(userId, username, _currentSessionId, 0, 1); // Idle minute
        }
        else
        {
            await _cache.IncrementUsageAsync(userId, username, _currentSessionId, 1, 0); // Active minute
        }

        var pendingRecords = await _cache.GetPendingRecordsAsync();
        
        if (pendingRecords.Count > 0)
        {
            // Have usage data to submit
            var response = await _syncService.SubmitUsageAsync(pendingRecords);
            if (response != null)
            {
                await _cache.MarkAsSyncedAsync(pendingRecords.Select(r => r.Id).ToList());
                await CheckEnforcementAsync(userId, response);
            }
            else
            {
                var cachedLimits = await _cache.GetLastKnownLimitsAsync(userId);
                if (cachedLimits != null)
                {
                    await CheckEnforcementAsync(userId, cachedLimits);
                }
            }
        }
        else
        {
            // No pending usage but user is logged in - check server for time limits
            // This handles the case where parent added time while child was logged off
            var response = await _syncService.CheckTimeRemainingAsync(username);
            if (response != null)
            {
                await CheckEnforcementAsync(userId, response);
            }
        }
    }

    private async Task CheckEnforcementAsync(Guid userId, UsageReportResponse limits)
    {
        var remaining = TimeSpan.FromMinutes(limits.TimeRemainingMinutes);

        if (limits.ShouldEnforce)
        {
            _logger.LogWarning("Enforcement required (time limit or allowed hours), enforcing logoff");
            _enforcement.LogoffUser();
        }
        else if (remaining <= TimeSpan.FromMinutes(5))
        {
            await _enforcement.ShowWarningAsync(remaining);
        }
    }
}
