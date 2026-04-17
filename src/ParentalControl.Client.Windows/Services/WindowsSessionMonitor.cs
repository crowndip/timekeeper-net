using System.Management;
using Microsoft.Win32;
using System.DirectoryServices.AccountManagement;

namespace ParentalControl.Client.Windows.Services;

public class WindowsSessionMonitor : ISessionMonitor
{
    private readonly ILogger<WindowsSessionMonitor> _logger;
    public event EventHandler<SessionChangeEventArgs>? SessionChanged;

    public WindowsSessionMonitor(ILogger<WindowsSessionMonitor> logger)
    {
        _logger = logger;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public string? GetCurrentUser()
    {
        try
        {
            var query = new SelectQuery("SELECT UserName FROM Win32_ComputerSystem");
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject mo in searcher.Get())
            {
                var username = mo["UserName"]?.ToString();
                if (!string.IsNullOrEmpty(username))
                {
                    var parts = username.Split('\\');
                    return parts.Length > 1 ? parts[1] : parts[0];
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
        }
        return null;
    }
    
    public async Task<List<string>> GetAllLocalUsersAsync()
    {
        var users = new List<string>();
        
        await Task.Run(() =>
        {
            try
            {
                using var context = new PrincipalContext(ContextType.Machine);
                using var searcher = new PrincipalSearcher(new UserPrincipal(context));
                
                foreach (var result in searcher.FindAll())
                {
                    if (result is UserPrincipal user && user.Enabled == true)
                    {
                        users.Add(user.SamAccountName);
                        _logger.LogDebug("Found local user: {Username}", user.SamAccountName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading local users");
            }
        });
        
        return users;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        var username = GetCurrentUser();
        var changeType = e.Reason switch
        {
            SessionSwitchReason.SessionLogon => SessionChangeType.Logon,
            SessionSwitchReason.SessionLogoff => SessionChangeType.Logoff,
            SessionSwitchReason.SessionLock => SessionChangeType.Lock,
            SessionSwitchReason.SessionUnlock => SessionChangeType.Unlock,
            _ => (SessionChangeType?)null
        };

        if (changeType.HasValue)
        {
            _logger.LogInformation("Session change: {Type} for user {User}", changeType, username);
            SessionChanged?.Invoke(this, new SessionChangeEventArgs(changeType.Value, username));
        }
    }
}
