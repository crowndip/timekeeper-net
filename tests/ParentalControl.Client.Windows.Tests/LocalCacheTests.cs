using ParentalControl.Client.Windows.Services;
using ParentalControl.Shared.DTOs;
using Xunit;

namespace ParentalControl.Client.Windows.Tests;

public class LocalCacheTests
{
    [Fact]
    public async Task IncrementUsage_AddsRecord()
    {
        var cache = new LocalCache();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await cache.IncrementUsageAsync(userId, sessionId, 5, 0);
        var records = await cache.GetPendingRecordsAsync();

        Assert.Single(records);
        Assert.Equal(userId, records[0].UserId);
        Assert.Equal(5, records[0].MinutesActive);
    }

    [Fact]
    public async Task GetTodayUsage_ReturnsAccumulatedMinutes()
    {
        var cache = new LocalCache();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await cache.IncrementUsageAsync(userId, sessionId, 10, 0);
        await cache.IncrementUsageAsync(userId, sessionId, 15, 0);
        var usage = await cache.GetTodayUsageAsync(userId, today);

        Assert.Equal(25, usage);
    }

    [Fact]
    public async Task SaveLastKnownLimits_StoresAndRetrieves()
    {
        var cache = new LocalCache();
        var userId = Guid.NewGuid();
        var limits = new UsageReportResponse(120, false, "logout", new[] { 15, 10, 5, 1 });

        await cache.SaveLastKnownLimitsAsync(userId, limits);
        var retrieved = await cache.GetLastKnownLimitsAsync(userId);

        Assert.NotNull(retrieved);
        Assert.Equal(120, retrieved.TimeRemainingMinutes);
    }

    [Fact]
    public async Task MarkAsSynced_RemovesRecords()
    {
        var cache = new LocalCache();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await cache.IncrementUsageAsync(userId, sessionId, 5, 0);
        var records = await cache.GetPendingRecordsAsync();
        var recordIds = records.Select(r => r.Id).ToList();

        await cache.MarkAsSyncedAsync(recordIds);
        var remaining = await cache.GetPendingRecordsAsync();

        Assert.Empty(remaining);
    }
}
