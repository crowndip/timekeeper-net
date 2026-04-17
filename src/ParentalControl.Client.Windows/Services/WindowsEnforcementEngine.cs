using System.Runtime.InteropServices;

namespace ParentalControl.Client.Windows.Services;

public class WindowsEnforcementEngine : IEnforcementEngine
{
    private readonly ILogger<WindowsEnforcementEngine> _logger;

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSLogoffSession(IntPtr hServer, int SessionId, bool bWait);

    [DllImport("kernel32.dll")]
    private static extern int WTSGetActiveConsoleSessionId();

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public WindowsEnforcementEngine(ILogger<WindowsEnforcementEngine> logger)
    {
        _logger = logger;
    }

    public void LogoffUser()
    {
        try
        {
            int sessionId = WTSGetActiveConsoleSessionId();
            _logger.LogWarning("Logging off user, session ID: {SessionId}", sessionId);
            WTSLogoffSession(IntPtr.Zero, sessionId, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logoff user");
        }
    }

    public void LockSession()
    {
        try
        {
            _logger.LogInformation("Locking workstation");
            LockWorkStation();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock session");
        }
    }

    public async Task ShowWarningAsync(TimeSpan timeRemaining)
    {
        try
        {
            _logger.LogWarning("Time remaining: {Minutes} minutes", timeRemaining.TotalMinutes);
            // TODO: Implement named pipe communication to UI
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show warning");
        }
    }
}
