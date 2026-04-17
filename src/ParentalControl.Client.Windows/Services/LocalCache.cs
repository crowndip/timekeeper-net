using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Windows.Services;

public interface ILocalCache
{
    Task IncrementUsageAsync(Guid userId, Guid sessionId, int activeMinutes, int idleMinutes);
    Task<List<UsageRecord>> GetPendingRecordsAsync();
    Task MarkAsSyncedAsync(List<Guid> recordIds);
    Task<ClientConfigResponse?> GetCachedConfigAsync();
    Task SaveConfigAsync(ClientConfigResponse config);
    Task<UsageReportResponse?> GetLastKnownLimitsAsync(Guid userId);
    Task SaveLastKnownLimitsAsync(Guid userId, UsageReportResponse limits);
    Task<int> GetTodayUsageAsync(Guid userId, DateOnly date);
}

public class LocalCache : ILocalCache
{
    private readonly List<UsageRecord> _records = new();
    private ClientConfigResponse? _cachedConfig;
    private readonly Dictionary<Guid, UsageReportResponse> _lastKnownLimits = new();
    private readonly Dictionary<string, int> _dailyUsage = new();
    
    public Task IncrementUsageAsync(Guid userId, Guid sessionId, int activeMinutes, int idleMinutes)
    {
        _records.Add(new UsageRecord(
            Guid.NewGuid(),
            userId,
            sessionId,
            activeMinutes,
            idleMinutes,
            DateTime.UtcNow,
            false
        ));
        
        var key = $"{userId}:{DateOnly.FromDateTime(DateTime.UtcNow)}";
        _dailyUsage.TryGetValue(key, out var current);
        _dailyUsage[key] = current + activeMinutes;
        
        return Task.CompletedTask;
    }
    
    public Task<List<UsageRecord>> GetPendingRecordsAsync()
    {
        return Task.FromResult(_records.Where(r => !r.Synced).ToList());
    }
    
    public Task MarkAsSyncedAsync(List<Guid> recordIds)
    {
        _records.RemoveAll(r => recordIds.Contains(r.Id));
        return Task.CompletedTask;
    }
    
    public Task<ClientConfigResponse?> GetCachedConfigAsync() => Task.FromResult(_cachedConfig);
    
    public Task SaveConfigAsync(ClientConfigResponse config)
    {
        _cachedConfig = config;
        return Task.CompletedTask;
    }
    
    public Task<UsageReportResponse?> GetLastKnownLimitsAsync(Guid userId)
    {
        _lastKnownLimits.TryGetValue(userId, out var limits);
        return Task.FromResult(limits);
    }
    
    public Task SaveLastKnownLimitsAsync(Guid userId, UsageReportResponse limits)
    {
        _lastKnownLimits[userId] = limits;
        return Task.CompletedTask;
    }
    
    public Task<int> GetTodayUsageAsync(Guid userId, DateOnly date)
    {
        var key = $"{userId}:{date}";
        _dailyUsage.TryGetValue(key, out var usage);
        return Task.FromResult(usage);
    }
}
