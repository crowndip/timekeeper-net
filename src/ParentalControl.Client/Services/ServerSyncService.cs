using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Services;

public interface IServerSyncService
{
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
    private const string ComputerIdPath = "/etc/parental-control/computer-id";
    private const string ServerUrlPath = "/etc/parental-control/server-url";
    
    public ServerSyncService(HttpClient httpClient, IConfiguration configuration, ILogger<ServerSyncService> logger, ILocalCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        
        // Load ComputerId from persistent storage
        _computerId = LoadComputerId();
        
        // Load server URL from persistent storage or config
        var serverUrl = LoadServerUrl() ?? configuration["ParentalControl:ServerUrl"];
        if (!string.IsNullOrEmpty(serverUrl))
        {
            SaveServerUrl(serverUrl); // Save for next time if from config
        }
        
        _httpClient.BaseAddress = new Uri(serverUrl!);
        _httpClient.Timeout = TimeSpan.FromSeconds(10); // Short timeout for offline detection
        
        // Configure Basic Authentication for reverse proxy
        var proxyEnabled = configuration.GetValue<bool>("ParentalControl:ReverseProxy:Enabled");
        if (proxyEnabled)
        {
            var username = configuration["ParentalControl:ReverseProxy:Username"];
            var password = configuration["ParentalControl:ReverseProxy:Password"];
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{username}:{password}");
                var authHeader = Convert.ToBase64String(authBytes);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
                _logger.LogInformation("Basic authentication configured for reverse proxy");
            }
        }
    }
    
    public async Task<UsageReportResponse?> SubmitUsageAsync(List<UsageRecord> records)
    {
        if (records.Count == 0) return null;

        try
        {
            if (!_computerId.HasValue)
            {
                _logger.LogWarning("ComputerId not configured, skipping usage submission");
                return null;
            }
            
            foreach (var record in records)
            {
                var request = new UsageReportRequest(
                    _computerId.Value,
                    record.UserId,
                    record.Username,
                    record.SessionId,
                    record.Timestamp,
                    record.MinutesActive,
                    record.MinutesIdle,
                    true
                );
                
                var response = await _httpClient.PostAsJsonAsync("/api/client/usage", request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
                    if (result != null)
                    {
                        // Cache the response for offline mode
                        await _cache.SaveLastKnownLimitsAsync(record.UserId, result);
                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to submit usage, status: {Status}", response.StatusCode);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error submitting usage, will use offline mode");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit usage");
        }
        
        return null;
    }
    
    public async Task<ClientConfigResponse?> GetConfigurationAsync()
    {
        try
        {
            var computerId = _configuration["ParentalControl:ComputerId"];
            return await _httpClient.GetFromJsonAsync<ClientConfigResponse>($"/api/client/config/{computerId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration");
            return null;
        }
    }
    
    public async Task<bool> RegisterComputerAsync()
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
                _logger.LogInformation("Registered with server: {ComputerId}", result.ComputerId);
                
                // Save to persistent storage
                SaveComputerId(result.ComputerId);
                
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register");
        }
        
        return false;
    }
    
    public async Task<UsageReportResponse?> CheckTimeRemainingAsync(string username)
    {
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
            
            var response = await _httpClient.PostAsJsonAsync("/api/client/usage", request);
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
        if (!_computerId.HasValue || usernames.Count == 0)
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
                
                await _httpClient.PostAsJsonAsync("/api/client/usage", request);
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
}
