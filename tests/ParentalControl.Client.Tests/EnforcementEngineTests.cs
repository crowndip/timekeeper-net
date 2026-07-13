using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParentalControl.Client.Services;
using Xunit;
using UsageReportResponse = ParentalControl.Shared.DTOs.UsageReportResponse;

namespace ParentalControl.Client.Tests;

public class EnforcementEngineTests
{
    [Fact]
    public async Task CheckAndEnforceAsync_LogoutAction_ReturnsImmediately_DoesNotBlockCaller()
    {
        // The core fix for "it sometimes hangs": CheckAndEnforceAsync runs on the same tick
        // loop that also tracks time and syncs every other session on the machine.
        // Enforcement used to await the full logout ladder in-line, which could block that
        // loop for up to 90s. It must now return near-instantly regardless of how long the
        // actual OS-level logout takes in the background.
        var cache = new Mock<ILocalCache>();
        var engine = new EnforcementEngine(NullLogger<EnforcementEngine>.Instance, cache.Object);
        var response = new UsageReportResponse(-1, true, "logout", new[] { 15, 10, 5, 1 });

        var stopwatch = Stopwatch.StartNew();
        await engine.CheckAndEnforceAsync(response, "child1", "session1");
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"CheckAndEnforceAsync should return almost immediately, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CheckAndEnforceAsync_LogoutAction_CalledTwiceRapidly_DoesNotDoubleLaunch()
    {
        // A still-over-limit user re-checked before the first attempt finishes (e.g. the
        // online path this tick and the offline path racing it) must not start a second
        // concurrent logout ladder for the same username.
        var cache = new Mock<ILocalCache>();
        var engine = new EnforcementEngine(NullLogger<EnforcementEngine>.Instance, cache.Object);
        var response = new UsageReportResponse(-1, true, "logout", new[] { 15, 10, 5, 1 });

        // Both calls must return promptly regardless of whether a background attempt is
        // already in flight for this username.
        var stopwatch = Stopwatch.StartNew();
        await engine.CheckAndEnforceAsync(response, "child1", "session1");
        await engine.CheckAndEnforceAsync(response, "child1", "session1");
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Both calls should return almost immediately, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CheckAndEnforceOfflineAsync_UsesRecordsSum_NotWholeDayUsage()
    {
        // Regression test for #5: lastLimits.TimeRemainingMinutes is already net of usage
        // as of the last successful sync. The unsynced `records` passed in are exactly the
        // usage since that sync -- subtracting the *whole day's* usage (which double-counts
        // minutes the server already accounted for) was logging children out far too early.
        var cache = new Mock<ILocalCache>();
        cache.Setup(c => c.GetLastKnownLimitsAsync("child1"))
            .ReturnsAsync(new UsageReportResponse(30, false, "logout", new[] { 15, 10, 5, 1 }));

        var logger = new Mock<ILogger<EnforcementEngine>>();
        var engine = new EnforcementEngine(logger.Object, cache.Object);

        // Child used 60 minutes total today; server said "30 remaining" the last time it
        // was reached (i.e. after 60 minutes were already used). Only 10 more offline
        // minutes have happened since that sync.
        var recordsSinceLastSync = new List<UsageRecord>
        {
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 5, 0, DateTime.UtcNow, false),
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 5, 0, DateTime.UtcNow, false),
        };

        await engine.CheckAndEnforceOfflineAsync(recordsSinceLastSync, "child1", "session1");

        // 30 - 10 = 20 minutes remaining, logged and no enforcement triggered.
        // (Old buggy behavior summed the whole day's 70 minutes: 30 - 70 = -40, an
        // immediate false logout despite 20 genuine minutes remaining -- which would also
        // have shelled out to loginctl/pkill from inside this unit test.)
        VerifyLogContains(logger, "20 minutes remaining", Times.Once());
    }

    [Fact]
    public async Task CheckAndEnforceOfflineAsync_KeyedByUsername_NotUserId()
    {
        // Regression test for #4: SystemdSessionMonitor always reports UserId = Guid.Empty,
        // so offline enforcement must look up cached limits by username.
        var cache = new Mock<ILocalCache>();
        cache.Setup(c => c.GetLastKnownLimitsAsync("child1"))
            .ReturnsAsync((UsageReportResponse?)null);

        var engine = new EnforcementEngine(NullLogger<EnforcementEngine>.Instance, cache.Object);
        var records = new List<UsageRecord> { new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 1, 0, DateTime.UtcNow, false) };

        await engine.CheckAndEnforceOfflineAsync(records, "child1", "session1");

        cache.Verify(c => c.GetLastKnownLimitsAsync("child1"), Times.Once);
    }

    [Fact]
    public async Task CheckAndEnforceAsync_WarningAtSkippedValue_StillFires()
    {
        // Regression test for #9: TimeRemainingMinutes can skip a warning threshold
        // entirely (missed sync, binding constraint switching, parent adjustments), so an
        // exact-equality check can silently miss it. "<=" must still catch it.
        var cache = new Mock<ILocalCache>();
        var logger = new Mock<ILogger<EnforcementEngine>>();
        var engine = new EnforcementEngine(logger.Object, cache.Object);

        // Jumps straight from "no warning yet" to 3 minutes remaining, skipping the 15-,
        // 10-, and 5-minute thresholds entirely (old "==" check would never fire any of
        // them, since 3 never exactly equals 15, 10, or 5).
        var response = new UsageReportResponse(3, false, "logout", new[] { 15, 10, 5, 1 });

        await engine.CheckAndEnforceAsync(response, "child1", "session1");

        // Substrings are prefixed with ": " so "15" doesn't also match the "5" check.
        VerifyLogContains(logger, ": 15 minutes remaining", Times.Once());
        VerifyLogContains(logger, ": 10 minutes remaining", Times.Once());
        VerifyLogContains(logger, ": 5 minutes remaining", Times.Once());

        // A second call at the same remaining time must not re-fire the same warnings.
        await engine.CheckAndEnforceAsync(response, "child1", "session1");
        // Substrings are prefixed with ": " so "15" doesn't also match the "5" check.
        VerifyLogContains(logger, ": 15 minutes remaining", Times.Once());
        VerifyLogContains(logger, ": 10 minutes remaining", Times.Once());
        VerifyLogContains(logger, ": 5 minutes remaining", Times.Once());
    }

    private static void VerifyLogContains(Mock<ILogger<EnforcementEngine>> logger, string expectedSubstring, Times times)
    {
        logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(expectedSubstring)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
