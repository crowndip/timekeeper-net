namespace ParentalControl.Client.Services;

public record UsageRecord(Guid Id, Guid UserId, Guid SessionId, int MinutesActive, int MinutesIdle, DateTime Timestamp, bool Synced);

public interface ITimeTracker
{
    Task RecordMinuteAsync(UserSession session);
    Task<List<UsageRecord>> GetPendingUsageAsync();
    Task MarkAsSyncedAsync(List<Guid> recordIds);
}

public class TimeTracker : ITimeTracker
{
    private readonly ILocalCache _cache;
    private readonly ISessionMonitor _sessionMonitor;
    
    public TimeTracker(ILocalCache cache, ISessionMonitor sessionMonitor)
    {
        _cache = cache;
        _sessionMonitor = sessionMonitor;
    }
    
    public async Task RecordMinuteAsync(UserSession session)
    {
        var isIdle = await _sessionMonitor.IsSessionIdleAsync(session.SessionId);
        
        await _cache.IncrementUsageAsync(
            session.UserId,
            Guid.Parse(session.SessionId),
            activeMinutes: isIdle ? 0 : 1,
            idleMinutes: isIdle ? 1 : 0
        );
    }
    
    public Task<List<UsageRecord>> GetPendingUsageAsync() => _cache.GetPendingRecordsAsync();
    
    public Task MarkAsSyncedAsync(List<Guid> recordIds) => _cache.MarkAsSyncedAsync(recordIds);
}
