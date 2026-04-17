using System.Diagnostics;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Services;

public interface IEnforcementEngine
{
    Task CheckAndEnforceAsync(UsageReportResponse response);
    Task CheckAndEnforceOfflineAsync(List<UsageRecord> records);
}

public class EnforcementEngine : IEnforcementEngine
{
    private readonly ILogger<EnforcementEngine> _logger;
    private readonly ILocalCache _cache;
    private readonly HashSet<int> _warningsShown = new();
    
    public EnforcementEngine(ILogger<EnforcementEngine> logger, ILocalCache cache)
    {
        _logger = logger;
        _cache = cache;
    }
    
    public async Task CheckAndEnforceAsync(UsageReportResponse response)
    {
        if (response.ShouldEnforce && !string.IsNullOrEmpty(response.EnforcementAction))
        {
            _logger.LogWarning("Enforcing action: {Action}", response.EnforcementAction);
            
            switch (response.EnforcementAction)
            {
                case "logout":
                    await LogoutCurrentUserAsync();
                    break;
                case "lock":
                    await LockSessionAsync();
                    break;
            }
        }
        
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
    
    public async Task CheckAndEnforceOfflineAsync(List<UsageRecord> records)
    {
        if (records.Count == 0) return;
        
        // Group by user
        var userRecords = records.GroupBy(r => r.UserId);
        
        foreach (var group in userRecords)
        {
            var userId = group.Key;
            var lastLimits = await _cache.GetLastKnownLimitsAsync(userId);
            
            if (lastLimits == null)
            {
                _logger.LogWarning("No cached limits for user {UserId}, cannot enforce offline", userId);
                continue;
            }
            
            // Calculate today's usage from cache
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var todayUsage = await _cache.GetTodayUsageAsync(userId, today);
            
            // Calculate remaining time based on last known limits
            var timeRemaining = lastLimits.TimeRemainingMinutes - todayUsage;
            
            _logger.LogInformation("Offline mode: User {UserId} has {TimeRemaining} minutes remaining (cached)", 
                userId, timeRemaining);
            
            if (timeRemaining <= 0)
            {
                _logger.LogWarning("Offline enforcement: Time limit reached for user {UserId}", userId);
                
                if (!string.IsNullOrEmpty(lastLimits.EnforcementAction))
                {
                    switch (lastLimits.EnforcementAction)
                    {
                        case "logout":
                            await LogoutCurrentUserAsync();
                            break;
                        case "lock":
                            await LockSessionAsync();
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
    }
    
    private async Task LogoutCurrentUserAsync()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "loginctl",
                Arguments = "terminate-user $USER",
                UseShellExecute = false
            });
            
            if (process != null)
            {
                await process.WaitForExitAsync();
                _logger.LogInformation("User logged out");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout user");
        }
    }
    
    private async Task LockSessionAsync()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "loginctl",
                Arguments = "lock-session",
                UseShellExecute = false
            });
            
            if (process != null)
            {
                await process.WaitForExitAsync();
                _logger.LogInformation("Session locked");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock session");
        }
    }
}
