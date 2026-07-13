using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Windows.Services;

public interface IServerSyncService
{
    // Reads config/persistent storage and prepares the HttpClient. Safe to call
    // repeatedly: it's a no-op once configured, and retries (rather than throwing) if
    // the server URL isn't available yet, so a missing/invalid config can never crash
    // the service -- it just keeps enforcement offline-only until config appears.
    Task InitializeAsync();
    Task<UsageReportResponse?> SubmitUsageAsync(List<UsageRecord> records);
    Task<ClientConfigResponse?> GetConfigurationAsync();
    Task<bool> RegisterComputerAsync();
    Task<UsageReportResponse?> CheckTimeRemainingAsync(string username);
    Task SyncAllUsersAsync(List<string> usernames);
}

public class ServerSyncService : IServerSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerSyncService> _logger;
    private readonly ILocalCache _cache;
    private Guid? _computerId;
    private string? _apiKey;
    private bool _configured;
    private const string ComputerIdPath = @"C:\ProgramData\ParentalControl\computer-id.txt";
    private const string ApiKeyPath = @"C:\ProgramData\ParentalControl\api-key.txt";
    private const string ServerUrlPath = @"C:\ProgramData\ParentalControl\server-url.txt";

    public ServerSyncService(HttpClient httpClient, IConfiguration configuration, ILogger<ServerSyncService> logger, ILocalCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    // All file I/O and URL parsing that used to live in the constructor moved here. A DI
    // constructor that throws (e.g. `new Uri(serverUrl!)` on a null/invalid URL) fails the
    // whole object graph build, which crash-loops the Windows service and means zero
    // enforcement until someone notices and fixes the config by hand.
    public Task InitializeAsync()
    {
        if (_configured) return Task.CompletedTask;

        _computerId ??= LoadComputerId();
        _apiKey ??= LoadApiKey();
        ApplyApiKeyHeader();

        var serverUrl = LoadServerUrl() ?? _configuration["ParentalControl:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            _logger.LogWarning("No server URL configured yet (neither {Path} nor ParentalControl:ServerUrl); will retry", ServerUrlPath);
            return Task.CompletedTask;
        }

        try
        {
            _httpClient.BaseAddress = new Uri(serverUrl);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex, "Configured server URL '{ServerUrl}' is not a valid URL; will retry", serverUrl);
            return Task.CompletedTask;
        }

        SaveServerUrl(serverUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Configure Basic Authentication for reverse proxy
        var proxyEnabled = _configuration.GetValue<bool>("ParentalControl:ReverseProxy:Enabled");
        if (proxyEnabled)
        {
            var username = _configuration["ParentalControl:ReverseProxy:Username"];
            var password = _configuration["ParentalControl:ReverseProxy:Password"];
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{username}:{password}");
                var authHeader = Convert.ToBase64String(authBytes);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
                _logger.LogInformation("Basic authentication configured for reverse proxy");
            }
        }

        _configured = true;
        _logger.LogInformation("ServerSyncService configured with base address {BaseAddress}", _httpClient.BaseAddress);
        return Task.CompletedTask;
    }

    public async Task<UsageReportResponse?> SubmitUsageAsync(List<UsageRecord> records)
    {
        if (records.Count == 0) return null;

        await InitializeAsync();
        if (!_configured)
        {
            _logger.LogWarning("Server not configured, skipping usage submission");
            return null;
        }

        if (!_computerId.HasValue)
        {
            _logger.LogWarning("ComputerId not configured");
            return null;
        }

        try
        {
            // Group by calendar date: a batch accumulated while offline can span local
            // midnight, and stamping the whole batch under one timestamp silently books
            // later minutes onto an earlier date. One request per date keeps each day's
            // minutes on the day they actually happened. All-or-nothing per call: if any
            // date's request fails, none of these records are reported synced (the caller
            // retries the whole batch next tick).
            //
            // Grouping here is by UTC date (r.Timestamp is UTC), while the server buckets
            // usage by household LOCAL date (see IClockService on the server). A batch
            // spanning local-but-not-UTC midnight can therefore still land in one UTC-date
            // group here and get split into two local-date rows server-side, or vice versa
            // -- a known, accepted ±1-2h edge case for offline batches, not a correctness
            // bug (the server is still the source of truth for which local day a minute
            // belongs to).
            var groups = records
                .GroupBy(r => DateOnly.FromDateTime(r.Timestamp))
                .OrderBy(g => g.Key)
                .ToList();

            UsageReportResponse? lastResponse = null;

            foreach (var group in groups)
            {
                var groupRecords = group.ToList();
                var totalActive = groupRecords.Sum(r => r.MinutesActive);
                var totalIdle = groupRecords.Sum(r => r.MinutesIdle);
                var firstRecord = groupRecords.First();

                var request = new UsageReportRequest(
                    _computerId.Value,
                    firstRecord.UserId,
                    firstRecord.Username,
                    firstRecord.SessionId,
                    firstRecord.Timestamp,
                    totalActive,
                    totalIdle,
                    true
                );

                var response = await SendWithReauthAsync(() => _httpClient.PostAsJsonAsync("/api/client/usage", request));
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to submit usage for {Date}, status: {Status}", group.Key, response.StatusCode);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
                if (result == null) return null;

                // Cache the response for offline mode (keyed by username -- see LocalCache).
                await _cache.SaveLastKnownLimitsAsync(firstRecord.Username, result);
                lastResponse = result;
            }

            return lastResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error, using offline mode");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit usage");
        }

        return null;
    }

    public async Task<ClientConfigResponse?> GetConfigurationAsync()
    {
        await InitializeAsync();
        if (!_configured) return null;

        if (!_computerId.HasValue)
        {
            _logger.LogWarning("ComputerId not configured, cannot fetch configuration");
            return null;
        }

        try
        {
            var response = await SendWithReauthAsync(() => _httpClient.GetAsync($"/api/client/config/{_computerId.Value}"));
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ClientConfigResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration");
            return null;
        }
    }

    public async Task<bool> RegisterComputerAsync()
    {
        await InitializeAsync();
        if (!_configured) return false;

        return await DoRegisterAsync();
    }

    // Registration is idempotent server-side (ClientController.Register matches on
    // MachineId and returns the existing ApiKey if the computer already exists), so
    // it's always safe to call this again -- both for the worker's normal startup
    // registration and to recover a fresh ApiKey after a 401 (see SendWithReauthAsync).
    private async Task<bool> DoRegisterAsync()
    {
        try
        {
            var hostname = Environment.MachineName;
            var machineId = GetMachineId();
            var osInfo = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            var request = new RegisterComputerRequest(hostname, machineId, osInfo);
            var response = await _httpClient.PostAsJsonAsync("/api/client/register", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterComputerResponse>();
                _computerId = result!.ComputerId;
                _apiKey = result.ApiKey;
                _logger.LogInformation("Registered: {ComputerId}", result.ComputerId);

                SaveComputerId(result.ComputerId);
                SaveApiKey(result.ApiKey);
                ApplyApiKeyHeader();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register");
        }

        return false;
    }

    // Sends a request; if the server rejects it with 401 (stale/rotated/wrong ApiKey,
    // or a fresh server DB with no memory of this computer), drops the stored key,
    // re-registers (which returns the current key), and retries exactly once. This is
    // what makes key rotation and server DB restores self-healing with zero manual steps.
    private async Task<HttpResponseMessage> SendWithReauthAsync(Func<Task<HttpResponseMessage>> send)
    {
        var response = await send();
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        _logger.LogWarning("Server rejected request with 401, re-registering to refresh API key");
        _apiKey = null;
        ApplyApiKeyHeader();

        if (!await DoRegisterAsync())
            return response;

        return await send();
    }

    private void ApplyApiKeyHeader()
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrEmpty(_apiKey))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
    }

    public async Task<UsageReportResponse?> CheckTimeRemainingAsync(string username)
    {
        await InitializeAsync();
        if (!_configured) return null;

        try
        {
            if (!_computerId.HasValue)
            {
                _logger.LogWarning("ComputerId not configured");
                return null;
            }
            
            var request = new UsageReportRequest(
                _computerId.Value,
                Guid.Empty,
                username,
                null, // No session ID
                DateTime.UtcNow,
                0,
                0,
                true
            );
            
            var response = await SendWithReauthAsync(() => _httpClient.PostAsJsonAsync("/api/client/usage", request));
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
                if (result != null)
                {
                    _logger.LogDebug("Time check for {Username}: {TimeRemaining} minutes",
                        username, result.TimeRemainingMinutes);
                    return result;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Network error checking time for {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check time for {Username}", username);
        }
        
        return null;
    }
    
    // MachineGuid is generated once by Windows Setup and stable across reinstalls of this
    // app, hostname renames, and joining/leaving a domain -- unlike the hostname, which a
    // parent might rename, or two machines might share by coincidence (both would then
    // silently share one server-side identity and ApiKey if hostname were used instead).
    private static string GetMachineId()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid") as string;
            if (!string.IsNullOrWhiteSpace(guid))
                return $"{guid}-WIN";
        }
        catch
        {
            // Fall through to the legacy hostname-based ID below.
        }

        // Legacy fallback -- also what pre-upgrade clients registered as, so a machine
        // that can't read the registry key for some reason still registers consistently.
        return $"{Environment.MachineName}-WIN";
    }

    private Guid? LoadComputerId()
    {
        try
        {
            if (File.Exists(ComputerIdPath))
            {
                var id = File.ReadAllText(ComputerIdPath).Trim();
                if (Guid.TryParse(id, out var guid))
                {
                    _logger.LogInformation("Loaded ComputerId from persistent storage");
                    return guid;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ComputerId");
        }
        return null;
    }
    
    private void SaveComputerId(Guid computerId)
    {
        try
        {
            var dir = Path.GetDirectoryName(ComputerIdPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
            
            File.WriteAllText(ComputerIdPath, computerId.ToString());
            _logger.LogInformation("Saved ComputerId to persistent storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ComputerId");
        }
    }

    private string? LoadApiKey()
    {
        try
        {
            if (File.Exists(ApiKeyPath))
            {
                var key = File.ReadAllText(ApiKeyPath).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    _logger.LogInformation("Loaded API key from persistent storage");
                    return key;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load API key");
        }
        return null;
    }

    private void SaveApiKey(string apiKey)
    {
        try
        {
            var dir = Path.GetDirectoryName(ApiKeyPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            File.WriteAllText(ApiKeyPath, apiKey);
            _logger.LogInformation("Saved API key to persistent storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save API key");
        }
    }


    private string? LoadServerUrl()
    {
        try
        {
            if (File.Exists(ServerUrlPath))
            {
                var url = File.ReadAllText(ServerUrlPath).Trim();
                if (!string.IsNullOrEmpty(url))
                {
                    _logger.LogInformation("Loaded server URL from persistent storage");
                    return url;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server URL");
        }
        return null;
    }
    
    private void SaveServerUrl(string serverUrl)
    {
        try
        {
            var dir = Path.GetDirectoryName(ServerUrlPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
            
            File.WriteAllText(ServerUrlPath, serverUrl);
        }
        catch { }
    }
    
    public async Task SyncAllUsersAsync(List<string> usernames)
    {
        await InitializeAsync();
        if (!_configured || !_computerId.HasValue || usernames.Count == 0)
            return;

        try
        {
            foreach (var username in usernames)
            {
                var request = new UsageReportRequest(
                    _computerId.Value,
                    Guid.Empty,
                    username,
                    null,
                    DateTime.UtcNow,
                    0,
                    0,
                    true
                );
                
                await SendWithReauthAsync(() => _httpClient.PostAsJsonAsync("/api/client/usage", request));
            }

            _logger.LogInformation("Synced {Count} users with server", usernames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync users");
        }
    }
}
