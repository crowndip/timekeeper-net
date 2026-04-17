namespace ParentalControl.Client.Windows.Services;

public interface IEnforcementEngine
{
    void LogoffUser();
    void LockSession();
    Task ShowWarningAsync(TimeSpan timeRemaining);
}
