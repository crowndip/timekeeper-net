using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ParentalControl.Client.Services;
using Xunit;

namespace ParentalControl.Client.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ProcessExceedsTimeout_KillsItAndReturnsFalse()
    {
        var psi = new ProcessStartInfo { FileName = "/bin/sleep", Arguments = "30" };

        var stopwatch = Stopwatch.StartNew();
        var (ok, output) = await ProcessRunner.RunAsync(psi, TimeSpan.FromSeconds(1), NullLogger.Instance);
        stopwatch.Stop();

        Assert.False(ok);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Should return shortly after the 1s timeout, took {stopwatch.Elapsed.TotalSeconds}s");
    }

    [Fact]
    public async Task RunAsync_NonexistentBinary_ReturnsFalseWithoutThrowing()
    {
        var psi = new ProcessStartInfo { FileName = "/this/binary/does/not/exist-12345", Arguments = "" };

        var (ok, output) = await ProcessRunner.RunAsync(psi, TimeSpan.FromSeconds(5), NullLogger.Instance);

        Assert.False(ok);
    }

    [Fact]
    public async Task RunAsync_SuccessfulProcess_ReturnsTrueWithOutput()
    {
        var psi = new ProcessStartInfo { FileName = "/bin/echo", Arguments = "hello" };

        var (ok, output) = await ProcessRunner.RunAsync(psi, TimeSpan.FromSeconds(5), NullLogger.Instance);

        Assert.True(ok);
        Assert.Contains("hello", output);
    }
}
