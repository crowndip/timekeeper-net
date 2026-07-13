using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParentalControl.Client.Windows.Services;
using ParentalControl.Shared.DTOs;
using Xunit;

namespace ParentalControl.Client.Windows.Tests;

public class LocalCacheTests : IDisposable
{
    private readonly string _cacheFilePath = Path.Combine(Path.GetTempPath(), $"pc-cache-test-{Guid.NewGuid():N}.json");

    private LocalCache CreateCache()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ParentalControl:CacheFilePath"] = _cacheFilePath })
            .Build();
        return new LocalCache(NullLogger<LocalCache>.Instance, configuration);
    }

    public void Dispose()
    {
        if (File.Exists(_cacheFilePath)) File.Delete(_cacheFilePath);
        var tmp = _cacheFilePath + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    [Fact]
    public async Task IncrementUsage_AddsRecord()
    {
        var cache = CreateCache();
        await cache.InitializeAsync();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await cache.IncrementUsageAsync(userId, "testuser", sessionId, 5, 0);
        var records = await cache.GetPendingRecordsAsync();

        Assert.Single(records);
        Assert.Equal(userId, records[0].UserId);
        Assert.Equal(5, records[0].MinutesActive);
    }

    [Fact]
    public async Task Username_IsCaseInsensitive()
    {
        var cache = CreateCache();
        await cache.InitializeAsync();
        var limits = new UsageReportResponse(120, false, "logout", new[] { 15, 10, 5, 1 });

        await cache.SaveLastKnownLimitsAsync("Alice", limits);
        var retrieved = await cache.GetLastKnownLimitsAsync("alice");

        Assert.NotNull(retrieved);
        Assert.Equal(120, retrieved.TimeRemainingMinutes);
    }

    [Fact]
    public async Task SaveLastKnownLimits_StoresAndRetrieves()
    {
        var cache = CreateCache();
        await cache.InitializeAsync();
        var limits = new UsageReportResponse(120, false, "logout", new[] { 15, 10, 5, 1 });

        await cache.SaveLastKnownLimitsAsync("testuser", limits);
        var retrieved = await cache.GetLastKnownLimitsAsync("testuser");

        Assert.NotNull(retrieved);
        Assert.Equal(120, retrieved.TimeRemainingMinutes);
    }

    [Fact]
    public async Task MarkAsSynced_RemovesRecords()
    {
        var cache = CreateCache();
        await cache.InitializeAsync();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await cache.IncrementUsageAsync(userId, "testuser", sessionId, 5, 0);
        var records = await cache.GetPendingRecordsAsync();
        var recordIds = records.Select(r => r.Id).ToList();

        await cache.MarkAsSyncedAsync(recordIds);
        var remaining = await cache.GetPendingRecordsAsync();

        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Persistence_SurvivesReboot_PendingRecordsAndLimitsReloadFromDisk()
    {
        // Regression test for #7: a reboot while the server is unreachable must not erase
        // unsynced minutes or the last-known limits used for offline enforcement.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ParentalControl:CacheFilePath"] = _cacheFilePath })
            .Build();

        var beforeReboot = new LocalCache(NullLogger<LocalCache>.Instance, configuration);
        await beforeReboot.InitializeAsync();

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await beforeReboot.IncrementUsageAsync(userId, "testuser", sessionId, 7, 0);
        await beforeReboot.SaveLastKnownLimitsAsync("testuser", new UsageReportResponse(45, false, "logout", new[] { 15, 10, 5, 1 }));

        // Simulate a reboot: a brand-new process, brand-new LocalCache instance, same file.
        var afterReboot = new LocalCache(NullLogger<LocalCache>.Instance, configuration);
        await afterReboot.InitializeAsync();

        var pending = await afterReboot.GetPendingRecordsAsync();
        Assert.Single(pending);
        Assert.Equal(7, pending[0].MinutesActive);

        var limits = await afterReboot.GetLastKnownLimitsAsync("testuser");
        Assert.NotNull(limits);
        Assert.Equal(45, limits.TimeRemainingMinutes);
    }

    [Fact]
    public async Task Persistence_CorruptFile_StartsEmptyInsteadOfThrowing()
    {
        await File.WriteAllTextAsync(_cacheFilePath, "{ this is not valid json ]]]");

        var cache = CreateCache();
        await cache.InitializeAsync(); // must not throw

        var pending = await cache.GetPendingRecordsAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Persistence_MissingFile_StartsEmpty()
    {
        var cache = CreateCache();
        await cache.InitializeAsync();

        var pending = await cache.GetPendingRecordsAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Persistence_OldCacheFileWithDailyUsageKey_LoadsGracefully()
    {
        // Regression test: DailyUsage was removed as dead state (nothing read
        // GetTodayUsageAsync in production since the offline-math fix). A cache file
        // written by an older client version still has that key; System.Text.Json
        // ignores unknown properties by default, so loading it must not throw or lose
        // the fields that still exist.
        var oldFormatJson = """
        {
            "Records": [],
            "CachedConfig": null,
            "LastKnownLimits": { "testuser": { "TimeRemainingMinutes": 30, "ShouldEnforce": false, "EnforcementAction": "logout", "WarningMinutes": [15, 10, 5, 1] } },
            "DailyUsage": { "testuser:2026-01-01": 42 }
        }
        """;
        await File.WriteAllTextAsync(_cacheFilePath, oldFormatJson);

        var cache = CreateCache();
        await cache.InitializeAsync(); // must not throw

        var limits = await cache.GetLastKnownLimitsAsync("testuser");
        Assert.NotNull(limits);
        Assert.Equal(30, limits.TimeRemainingMinutes);
    }
}
