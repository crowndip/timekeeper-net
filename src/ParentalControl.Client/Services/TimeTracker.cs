namespace ParentalControl.Client.Services;

public record UsageRecord(Guid Id, Guid UserId, string Username, Guid SessionId, int MinutesActive, int MinutesIdle, DateTime Timestamp, bool Synced);

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
        
        // Generate a consistent Guid from the session ID string
        var sessionGuid = GenerateGuidFromString(session.SessionId);
        
        await _cache.IncrementUsageAsync(
            session.UserId,
            session.Username,
            sessionGuid,
            activeMinutes: isIdle ? 0 : 1,
            idleMinutes: isIdle ? 1 : 0
        );
    }
    
    private static Guid GenerateGuidFromString(string input)
    {
        // Create a deterministic Guid from a string
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
    
    public Task<List<UsageRecord>> GetPendingUsageAsync() => _cache.GetPendingRecordsAsync();
    
    public Task MarkAsSyncedAsync(List<Guid> recordIds) => _cache.MarkAsSyncedAsync(recordIds);
}
