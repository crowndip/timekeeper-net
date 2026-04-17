using System;
using System.Reactive;
using ReactiveUI;

namespace ParentalControl.Client.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _timeRemaining;
    private string _username = "User";
    private bool _isWarning;
    
    public int TimeRemaining
    {
        get => _timeRemaining;
        set => this.RaiseAndSetIfChanged(ref _timeRemaining, value);
    }
    
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }
    
    public bool IsWarning
    {
        get => _isWarning;
        set => this.RaiseAndSetIfChanged(ref _isWarning, value);
    }
    
    public string TimeRemainingText => $"{TimeRemaining / 60}h {TimeRemaining % 60}m remaining";
    
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    
    public MainWindowViewModel()
    {
        CloseCommand = ReactiveCommand.Create(() => { });
        
        // Simulate time updates
        TimeRemaining = 120;
    }
}
