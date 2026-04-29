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
            return;

        // Derive a stable userId from username so each user has their own cache entry (Bug #5)
        var userId = GetUserIdFromUsername(username);

        // Record time for current user only
        if (_isLocked)
            await _cache.IncrementUsageAsync(userId, username, _currentSessionId, 0, 1);
        else
            await _cache.IncrementUsageAsync(userId, username, _currentSessionId, 1, 0);

        // Only submit records belonging to the current user to avoid cross-user enforcement (Bug #2)
        var allPending = await _cache.GetPendingRecordsAsync();
        var userPending = allPending.Where(r => r.Username == username).ToList();

        if (userPending.Count > 0)
        {
            var response = await _syncService.SubmitUsageAsync(userPending);
            if (response != null)
            {
                await _cache.MarkAsSyncedAsync(userPending.Select(r => r.Id).ToList());
                await CheckEnforcementAsync(username, response);
            }
            else
            {
                // Offline: recalculate remaining time from cached limits minus today's usage (Bug #4)
                // This avoids permanent lockout from a stale ShouldEnforce=true snapshot
                var cachedLimits = await _cache.GetLastKnownLimitsAsync(userId);
                if (cachedLimits != null)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var todayUsage = await _cache.GetTodayUsageAsync(userId, today);
                    var adjustedRemaining = cachedLimits.TimeRemainingMinutes - todayUsage;

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

    // Derive a stable, deterministic Guid from a username (Bug #5)
    private static Guid GetUserIdFromUsername(string username)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(username.ToLowerInvariant()));
        return new Guid(hash);
    }
}
