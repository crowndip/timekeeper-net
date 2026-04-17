using Microsoft.Extensions.Logging;
using Moq;
using ParentalControl.Client.Windows.Services;
using Xunit;

namespace ParentalControl.Client.Windows.Tests;

public class WindowsEnforcementEngineTests
{
    [Fact]
    public void LogoffUser_DoesNotThrow()
    {
        var mockLogger = new Mock<ILogger<WindowsEnforcementEngine>>();
        var engine = new WindowsEnforcementEngine(mockLogger.Object);

        var exception = Record.Exception(() => engine.LogoffUser());

        Assert.Null(exception);
    }

    [Fact]
    public void LockSession_DoesNotThrow()
    {
        var mockLogger = new Mock<ILogger<WindowsEnforcementEngine>>();
        var engine = new WindowsEnforcementEngine(mockLogger.Object);

        var exception = Record.Exception(() => engine.LockSession());

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShowWarningAsync_DoesNotThrow()
    {
        var mockLogger = new Mock<ILogger<WindowsEnforcementEngine>>();
        var engine = new WindowsEnforcementEngine(mockLogger.Object);

        var exception = await Record.ExceptionAsync(async () => 
            await engine.ShowWarningAsync(TimeSpan.FromMinutes(5)));

        Assert.Null(exception);
    }
}
