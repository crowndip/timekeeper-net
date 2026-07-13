using System.Text.Json;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Windows.Services;

public record UsageRecord(Guid Id, Guid UserId, string Username, Guid SessionId, int MinutesActive, int MinutesIdle, DateTime Timestamp, bool Synced);

public interface ILocalCache
{
    // Loads persisted state from disk. Safe to call multiple times; a missing or
    // corrupt cache file is treated as "start empty", never as a fatal error.
    Task InitializeAsync();
    Task IncrementUsageAsync(Guid userId, string username, Guid sessionId, int activeMinutes, int idleMinutes);
    Task<List<UsageRecord>> GetPendingRecordsAsync();
    Task MarkAsSyncedAsync(List<Guid> recordIds);
    Task<ClientConfigResponse?> GetCachedConfigAsync();
    Task SaveConfigAsync(ClientConfigResponse config);
    Task<UsageReportResponse?> GetLastKnownLimitsAsync(string username);
    Task SaveLastKnownLimitsAsync(string username, UsageReportResponse limits);
}

// Persists pending usage, last-known limits, and daily usage to disk so a reboot while
// the server is unreachable doesn't erase unsynced minutes and doesn't leave offline
// enforcement with no cached limits. Keyed by normalized username (not a derived Guid):
// on this machine only one console user is tracked at a time, but keying by username
// keeps this class identical in shape to the Linux client's LocalCache (both back the
// same ILocalCache-shaped contract ahead of unifying the two clients).
public class LocalCache : ILocalCache
{
    private static readonly TimeSpan PendingRecordRetention = TimeSpan.FromDays(7);

    private readonly ILogger<LocalCache> _logger;
    private readonly string _cacheFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private List<UsageRecord> _records = new();
    private ClientConfigResponse? _cachedConfig;
    private Dictionary<string, UsageReportResponse> _lastKnownLimits = new();
    private bool _initialized;

    public LocalCache(ILogger<LocalCache> logger, IConfiguration configuration)
    {
        _logger = logger;
        _cacheFilePath = configuration["ParentalControl:CacheFilePath"] ?? @"C:\ProgramData\ParentalControl\cache.json";
    }

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_initialized) return;
            await LoadAsync();
            PruneExpiredLocked();
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task IncrementUsageAsync(Guid userId, string username, Guid sessionId, int activeMinutes, int idleMinutes)
    {
        await _lock.WaitAsync();
        try
        {
            _records.Add(new UsageRecord(
                Guid.NewGuid(),
                userId,
                username,
                sessionId,
                activeMinutes,
                idleMinutes,
                DateTime.UtcNow,
                false
            ));

            await SaveLocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<UsageRecord>> GetPendingRecordsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _records.Where(r => !r.Synced).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkAsSyncedAsync(List<Guid> recordIds)
    {
        await _lock.WaitAsync();
        try
        {
            _records.RemoveAll(r => recordIds.Contains(r.Id));
            await SaveLocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ClientConfigResponse?> GetCachedConfigAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _cachedConfig;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigAsync(ClientConfigResponse config)
    {
        await _lock.WaitAsync();
        try
        {
            _cachedConfig = config;
            await SaveLocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<UsageReportResponse?> GetLastKnownLimitsAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            _lastKnownLimits.TryGetValue(NormalizeUsername(username), out var limits);
            return limits;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveLastKnownLimitsAsync(string username, UsageReportResponse limits)
    {
        await _lock.WaitAsync();
        try
        {
            _lastKnownLimits[NormalizeUsername(username)] = limits;
            await SaveLocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    private void PruneExpiredLocked()
    {
        var recordCutoff = DateTime.UtcNow - PendingRecordRetention;
        var before = _records.Count;
        _records = _records.Where(r => r.Timestamp >= recordCutoff).ToList();
        if (before != _records.Count)
            _logger.LogInformation("Pruned {Count} stale pending usage records older than {Retention}", before - _records.Count, PendingRecordRetention);
    }

    private async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                _logger.LogInformation("No cache file at {Path}, starting with empty local cache", _cacheFilePath);
                return;
            }

            await using var stream = File.OpenRead(_cacheFilePath);
            var persisted = await JsonSerializer.DeserializeAsync<PersistedCacheState>(stream);
            if (persisted == null) return;

            _records = persisted.Records ?? new List<UsageRecord>();
            _cachedConfig = persisted.CachedConfig;
            _lastKnownLimits = persisted.LastKnownLimits ?? new Dictionary<string, UsageReportResponse>();

            _logger.LogInformation("Loaded local cache from {Path}: {PendingCount} pending records, {LimitCount} cached limits",
                _cacheFilePath, _records.Count(r => !r.Synced), _lastKnownLimits.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local cache at {Path} is missing or corrupt, starting with empty cache", _cacheFilePath);
            _records = new List<UsageRecord>();
            _cachedConfig = null;
            _lastKnownLimits = new Dictionary<string, UsageReportResponse>();
        }
    }

    // Writes to a temp file in the same directory, then renames over the real path, so a
    // crash or power loss mid-write can never leave a half-written cache file behind.
    private async Task SaveLocked()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var state = new PersistedCacheState
            {
                Records = _records,
                CachedConfig = _cachedConfig,
                LastKnownLimits = _lastKnownLimits
            };

            var tempPath = _cacheFilePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, state);
                // Force the write to physical disk before the rename below: without this,
                // a power loss could let the rename survive (it's just a directory-entry
                // update) while the file's actual content did not, corrupting the cache.
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, _cacheFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Persistence is best-effort: losing the on-disk cache is recoverable (we keep
            // running with the in-memory state), but the service must never crash over it.
            _logger.LogError(ex, "Failed to persist local cache to {Path}", _cacheFilePath);
        }
    }

    private class PersistedCacheState
    {
        public List<UsageRecord> Records { get; set; } = new();
        public ClientConfigResponse? CachedConfig { get; set; }
        public Dictionary<string, UsageReportResponse> LastKnownLimits { get; set; } = new();
    }
}
