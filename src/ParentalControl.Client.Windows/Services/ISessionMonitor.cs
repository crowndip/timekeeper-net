namespace ParentalControl.Client.Windows.Services;

public interface ISessionMonitor
{
    string? GetCurrentUser();
    event EventHandler<SessionChangeEventArgs>? SessionChanged;
}

public class SessionChangeEventArgs : EventArgs
{
    public SessionChangeType ChangeType { get; }
    public string? Username { get; }

    public SessionChangeEventArgs(SessionChangeType changeType, string? username)
    {
        ChangeType = changeType;
        Username = username;
    }
}

public enum SessionChangeType
{
    Logon,
    Logoff,
    Lock,
    Unlock
}
