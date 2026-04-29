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

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    public WindowsEnforcementEngine(ILogger<WindowsEnforcementEngine> logger)
    {
        _logger = logger;
    }

    public void LogoffUser()
    {
        try
        {
            int sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == -1) // 0xFFFFFFFF: no user at the physical console
            {
                _logger.LogWarning("No active console session to log off");
                return;
            }
            _logger.LogWarning("Logging off user, session ID: {SessionId}", sessionId);
            bool success = WTSLogoffSession(IntPtr.Zero, sessionId, false);
            if (!success)
                _logger.LogError("WTSLogoffSession failed, Win32 error: {Error}", Marshal.GetLastWin32Error());
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
            var minutes = (int)Math.Ceiling(timeRemaining.TotalMinutes);
            _logger.LogInformation("Showing warning: {Minutes} minutes remaining", minutes);

            int consoleSession = WTSGetActiveConsoleSessionId();
            if (consoleSession == -1)
            {
                _logger.LogWarning("No active console session, cannot show warning");
                return;
            }

            if (!WTSQueryUserToken((uint)consoleSession, out var userToken))
            {
                _logger.LogWarning("Could not get user token for session {Session}, Win32 error: {Error}",
                    consoleSession, Marshal.GetLastWin32Error());
                return;
            }

            try
            {
                CreateEnvironmentBlock(out var envBlock, userToken, false);
                try
                {
                    var uiExe = Path.Combine(AppContext.BaseDirectory, "ParentalControl.Client.Windows.UI.exe");
                    var cmdLine = $"\"{uiExe}\" --minutes {minutes}";
                    var si = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFO>(),
                        lpDesktop = "winsta0\\default"
                    };

                    if (!CreateProcessAsUser(userToken, null, cmdLine, IntPtr.Zero, IntPtr.Zero,
                            false, CREATE_UNICODE_ENVIRONMENT, envBlock, null, ref si, out var pi))
                    {
                        _logger.LogError("CreateProcessAsUser failed, Win32 error: {Error}",
                            Marshal.GetLastWin32Error());
                    }
                    else
                    {
                        CloseHandle(pi.hProcess);
                        CloseHandle(pi.hThread);
                    }
                }
                finally
                {
                    if (envBlock != IntPtr.Zero)
                        DestroyEnvironmentBlock(envBlock);
                }
            }
            finally
            {
                CloseHandle(userToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show warning");
        }

        await Task.CompletedTask;
    }
}
