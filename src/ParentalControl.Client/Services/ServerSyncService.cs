using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Services;

public interface IServerSyncService
{
    // Reads config/persistent storage and prepares the HttpClient. Safe to call
    // repeatedly: it's a no-op once configured, and retries (rather than throwing) if
    // the server URL isn't available yet, so a missing/invalid config can never crash
    // the daemon -- it just keeps enforcement offline-only until config appears.
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
    private const string ComputerIdPath = "/etc/parental-control/computer-id";
    private const string ApiKeyPath = "/etc/parental-control/api-key";
    private const string ServerUrlPath = "/etc/parental-control/server-url";
    private const string ProxyUserPath = "/etc/parental-control/proxy-user";
    private const string ProxyPassPath = "/etc/parental-control/proxy-pass";

    public ServerSyncService(HttpClient httpClient, IConfiguration configuration, ILogger<ServerSyncService> logger, ILocalCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    // All file I/O and URL parsing that used to live in the constructor moved here.
    // A DI constructor that throws (e.g. `new Uri(serverUrl!)` on a null/invalid URL)
    // fails the whole object graph build, which crash-loops the systemd service and
    // means zero enforcement until someone notices and fixes the config by hand.
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

        SaveServerUrl(serverUrl); // Save for next time if it came from config
        _httpClient.Timeout = TimeSpan.FromSeconds(10); // Short timeout for offline detection

        // Configure Basic Authentication for reverse proxy. Try persistent storage first, then config.
        var username = LoadProxyUser() ?? _configuration["ParentalControl:ReverseProxy:Username"];
        var password = LoadProxyPass() ?? _configuration["ParentalControl:ReverseProxy:Password"];

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            if (_configuration["ParentalControl:ReverseProxy:Username"] != null)
            {
                SaveProxyUser(username);
                SaveProxyPass(password);
            }

            var authBytes = Encoding.ASCII.GetBytes($"{username}:{password}");
            var authHeader = Convert.ToBase64String(authBytes);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
            _logger.LogInformation("Basic authentication configured for reverse proxy");
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
            _logger.LogWarning("ComputerId not configured, skipping usage submission");
            return null;
        }

        try
        {
            // Group by calendar date: a batch accumulated while offline can span local
            // midnight, and stamping the whole batch with the first record's timestamp
            // silently books later minutes onto an earlier date. One request per date
            // keeps each day's minutes on the day they actually happened. All-or-nothing
            // per call: if any date's request fails, none of these records are marked
            // synced (the caller retries the whole batch next tick), so a partial success
            // can never desync "which records are synced" from "what the server recorded".
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
            _logger.LogWarning(ex, "Network error submitting usage, will use offline mode");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit usage");
            return null;
        }
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
            var osInfo = Environment.OSVersion.ToString();

            var request = new RegisterComputerRequest(hostname, machineId, osInfo);
            var response = await _httpClient.PostAsJsonAsync("/api/client/register", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterComputerResponse>();
                _computerId = result!.ComputerId;
                _apiKey = result.ApiKey;
                _logger.LogInformation("Registered with server: {ComputerId}", result.ComputerId);

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

            // Submit zero usage just to check time remaining
            var request = new UsageReportRequest(
                _computerId.Value,
                Guid.Empty, // Server will determine from username
                username,
                null, // No session ID
                DateTime.UtcNow,
                0, // No usage
                0,
                true
            );

            var response = await SendWithReauthAsync(() => _httpClient.PostAsJsonAsync("/api/client/usage", request));
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
                if (result != null)
                {
                    _logger.LogDebug("Time check for {Username}: {TimeRemaining} minutes remaining",
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
            _logger.LogInformation("Attempting to save ComputerId to {Path}, directory: {Dir}", ComputerIdPath, dir);
            
            if (!Directory.Exists(dir))
            {
                _logger.LogInformation("Creating directory: {Dir}", dir);
                Directory.CreateDirectory(dir!);
            }
            
            File.WriteAllText(ComputerIdPath, computerId.ToString());
            _logger.LogInformation("Saved ComputerId to persistent storage: {Path}", ComputerIdPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ComputerId to {Path}", ComputerIdPath);
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
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(ApiKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

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
            _logger.LogInformation("Attempting to save server URL to {Path}, directory: {Dir}", ServerUrlPath, dir);
            
            if (!Directory.Exists(dir))
            {
                _logger.LogInformation("Creating directory: {Dir}", dir);
                Directory.CreateDirectory(dir!);
            }
            
            File.WriteAllText(ServerUrlPath, serverUrl);
            _logger.LogInformation("Saved server URL to persistent storage: {Path}", ServerUrlPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save server URL to {Path}", ServerUrlPath);
        }
    }
    
    private string? LoadProxyUser()
    {
        try
        {
            if (File.Exists(ProxyUserPath))
            {
                var user = File.ReadAllText(ProxyUserPath).Trim();
                if (!string.IsNullOrEmpty(user))
                {
                    _logger.LogInformation("Loaded proxy username from persistent storage");
                    return user;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load proxy username");
        }
        return null;
    }
    
    private void SaveProxyUser(string username)
    {
        try
        {
            var dir = Path.GetDirectoryName(ProxyUserPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
            
            File.WriteAllText(ProxyUserPath, username);
            _logger.LogInformation("Saved proxy username to persistent storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save proxy username");
        }
    }
    
    private string? LoadProxyPass()
    {
        try
        {
            if (File.Exists(ProxyPassPath))
            {
                var pass = File.ReadAllText(ProxyPassPath).Trim();
                if (!string.IsNullOrEmpty(pass))
                {
                    _logger.LogInformation("Loaded proxy password from persistent storage");
                    return pass;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load proxy password");
        }
        return null;
    }
    
    private void SaveProxyPass(string password)
    {
        try
        {
            var dir = Path.GetDirectoryName(ProxyPassPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            if (OperatingSystem.IsLinux())
            {
                // Set the restrictive mode as part of file creation (passed straight to the
                // open() syscall) rather than writing the content first and chmod-ing after --
                // the latter leaves the file world-readable under the default umask for the
                // brief window between creation and the chmod call.
                var options = new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead
                };
                using (var stream = new FileStream(ProxyPassPath, options))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(password);
                }

                // The tray app user must be a member of the parental-control group (set up
                // by the installer). Only warn on failure -- during a fresh install this can
                // legitimately run before that group exists yet.
                if (!SetFileGroup(ProxyPassPath, "parental-control"))
                {
                    _logger.LogWarning(
                        "Failed to set group ownership of {Path} to 'parental-control' (group may not exist yet); " +
                        "the tray app may not be able to read the proxy password until this is retried",
                        ProxyPassPath);
                }
            }
            else
            {
                File.WriteAllText(ProxyPassPath, password);
            }

            _logger.LogInformation("Saved proxy password to persistent storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save proxy password");
        }
    }
    
    public async Task SyncAllUsersAsync(List<string> usernames)
    {
        await InitializeAsync();
        if (!_configured || !_computerId.HasValue || usernames.Count == 0)
            return;

        try
        {
            // Send zero-usage requests for all users to register them with server
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
    
    private static string GetMachineId()
    {
        try
        {
            return File.ReadAllText("/etc/machine-id").Trim();
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private static bool SetFileGroup(string path, string group)
    {
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chgrp",
                Arguments = $"{group} {path}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p == null) return false;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
