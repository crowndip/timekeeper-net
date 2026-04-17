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
        
        // TODO: Get actual time from service via named pipe
        _timeRemaining = TimeSpan.FromMinutes(5);
        UpdateDisplay();
        _timer.Start();
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
