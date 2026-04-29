using System.Diagnostics;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Services;

public interface IEnforcementEngine
{
    Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId);
    Task CheckAndEnforceOfflineAsync(List<UsageRecord> records, string username, string sessionId);
}

public class EnforcementEngine : IEnforcementEngine
{
    private readonly ILogger<EnforcementEngine> _logger;
    private readonly ILocalCache _cache;
    private readonly HashSet<int> _warningsShown = new();
    private int _lastTimeRemaining = int.MaxValue;
    
    public EnforcementEngine(ILogger<EnforcementEngine> logger, ILocalCache cache)
    {
        _logger = logger;
        _cache = cache;
    }
    
    public async Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId)
    {
        if (response.ShouldEnforce && !string.IsNullOrEmpty(response.EnforcementAction))
        {
            _logger.LogWarning("Enforcing action: {Action} for user {Username}", response.EnforcementAction, username);

            switch (response.EnforcementAction)
            {
                case "logout":
                    await LogoutUserAsync(username);
                    break;
                case "lock":
                    await LockSessionAsync(sessionId);
                    break;
            }
        }
        
        // Reset warnings when time increases (new day or parent added time)
        if (response.TimeRemainingMinutes > _lastTimeRemaining)
        {
            _logger.LogInformation("Time increased from {Old} to {New} minutes, resetting warnings",
                _lastTimeRemaining, response.TimeRemainingMinutes);
            _warningsShown.Clear();
        }
        _lastTimeRemaining = response.TimeRemainingMinutes;

        foreach (var warningMinutes in response.WarningMinutes)
        {
            if (response.TimeRemainingMinutes == warningMinutes && !_warningsShown.Contains(warningMinutes))
            {
                _logger.LogInformation("Warning: {Minutes} minutes remaining", warningMinutes);
                _warningsShown.Add(warningMinutes);
                // TODO: Show notification via UI
            }
        }
    }
    
    public async Task CheckAndEnforceOfflineAsync(List<UsageRecord> records, string username, string sessionId)
    {
        if (records.Count == 0) return;

        var userId = records[0].UserId;
        var lastLimits = await _cache.GetLastKnownLimitsAsync(userId);

        if (lastLimits == null)
        {
            _logger.LogWarning("No cached limits for user {Username}, cannot enforce offline", username);
            return;
        }

        // Calculate today's usage from cache
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayUsage = await _cache.GetTodayUsageAsync(userId, today);

        // Calculate remaining time based on last known limits
        var timeRemaining = lastLimits.TimeRemainingMinutes - todayUsage;

        _logger.LogInformation("Offline mode: User {Username} has {TimeRemaining} minutes remaining (cached)",
            username, timeRemaining);

        if (timeRemaining <= 0)
        {
            _logger.LogWarning("Offline enforcement: Time limit reached for user {Username}", username);

            if (!string.IsNullOrEmpty(lastLimits.EnforcementAction))
            {
                switch (lastLimits.EnforcementAction)
                {
                    case "logout":
                        await LogoutUserAsync(username);
                        break;
                    case "lock":
                        await LockSessionAsync(sessionId);
                        break;
                }
            }
        }
        else
        {
            // Check warnings
            foreach (var warningMinutes in lastLimits.WarningMinutes)
            {
                if (timeRemaining <= warningMinutes && !_warningsShown.Contains(warningMinutes))
                {
                    _logger.LogInformation("Offline warning: {Minutes} minutes remaining", timeRemaining);
                    _warningsShown.Add(warningMinutes);
                }
            }
        }
    }
    
    private async Task LogoutUserAsync(string username)
    {
        try
        {
            _logger.LogInformation("Logging out user {Username}", username);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "loginctl",
                Arguments = $"terminate-user {username}",
                UseShellExecute = false
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    _logger.LogError("loginctl terminate-user {Username} exited with code {ExitCode}", username, process.ExitCode);
                else
                    _logger.LogInformation("User {Username} logged out", username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout user {Username}", username);
        }
    }

    private async Task LockSessionAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation("Locking session {SessionId}", sessionId);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "loginctl",
                Arguments = $"lock-session {sessionId}",
                UseShellExecute = false
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    _logger.LogError("loginctl lock-session {SessionId} exited with code {ExitCode}", sessionId, process.ExitCode);
                else
                    _logger.LogInformation("Session {SessionId} locked", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock session {SessionId}", sessionId);
        }
    }
}
