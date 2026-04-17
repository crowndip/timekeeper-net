namespace ParentalControl.Client.Services;

public record UserSession(Guid UserId, string Username, string SessionId, bool IsIdle);

public interface ISessionMonitor
{
    Task<List<UserSession>> GetActiveSessionsAsync();
    Task<bool> IsSessionIdleAsync(string sessionId);
}

public class SystemdSessionMonitor : ISessionMonitor
{
    private readonly ILogger<SystemdSessionMonitor> _logger;
    
    public SystemdSessionMonitor(ILogger<SystemdSessionMonitor> logger) => _logger = logger;
    
    public async Task<List<UserSession>> GetActiveSessionsAsync()
    {
        // TODO: Implement D-Bus integration with systemd-logind
        // For now, return empty list
        await Task.CompletedTask;
        return new List<UserSession>();
    }
    
    public async Task<bool> IsSessionIdleAsync(string sessionId)
    {
        // TODO: Check idle state via D-Bus
        await Task.CompletedTask;
        return false;
    }
}
