using Microsoft.Extensions.Logging;
using Moq;
using ParentalControl.Client.Windows.Services;
using Xunit;

namespace ParentalControl.Client.Windows.Tests;

public class WindowsSessionMonitorTests
{
    [Fact(Skip = "Requires Windows platform")]
    public void GetCurrentUser_ReturnsString()
    {
        var mockLogger = new Mock<ILogger<WindowsSessionMonitor>>();
        var monitor = new WindowsSessionMonitor(mockLogger.Object);

        var user = monitor.GetCurrentUser();

        Assert.True(user == null || user.Length > 0);
    }

    [Fact(Skip = "Requires Windows platform")]
    public void SessionChanged_EventExists()
    {
        var mockLogger = new Mock<ILogger<WindowsSessionMonitor>>();
        var monitor = new WindowsSessionMonitor(mockLogger.Object);
        var eventRaised = false;

        monitor.SessionChanged += (sender, args) => eventRaised = true;

        Assert.False(eventRaised);
    }
}
