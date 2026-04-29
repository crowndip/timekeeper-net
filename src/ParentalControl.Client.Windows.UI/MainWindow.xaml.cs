using System.Windows;
using System.Windows.Threading;

namespace ParentalControl.Client.Windows.UI;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private TimeSpan _timeRemaining;

    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        // Read --minutes <N> from command-line (passed by the service via CreateProcessAsUser)
        _timeRemaining = ParseTimeFromArgs() ?? TimeSpan.FromMinutes(5);
        UpdateDisplay();
        _timer.Start();
    }

    private static TimeSpan? ParseTimeFromArgs()
    {
        var args = Environment.GetCommandLineArgs();
        var idx = Array.IndexOf(args, "--minutes");
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var minutes))
            return TimeSpan.FromMinutes(minutes);
        return null;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_timeRemaining > TimeSpan.Zero)
        {
            _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
            UpdateDisplay();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void UpdateDisplay()
    {
        TimeRemainingText.Text = $"{(int)_timeRemaining.TotalMinutes}:{_timeRemaining.Seconds:D2}";
    }

    public void SetTimeRemaining(TimeSpan time)
    {
        _timeRemaining = time;
        UpdateDisplay();
    }
}
