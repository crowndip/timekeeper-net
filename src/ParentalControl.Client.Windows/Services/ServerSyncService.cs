using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Windows.Services;

public interface IServerSyncService
{
    Task<UsageReportResponse?> SubmitUsageAsync(List<UsageRecord> records);
    Task<ClientConfigResponse?> GetConfigurationAsync();
    Task<bool> RegisterComputerAsync();
    Task<UsageReportResponse?> CheckTimeRemainingAsync(string username);
}

public class ServerSyncService : IServerSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerSyncService> _logger;
    private readonly ILocalCache _cache;
    private Guid? _computerId;
    private const string ComputerIdPath = @"C:\ProgramData\ParentalControl\computer-id.txt";
    private const string ServerUrlPath = @"C:\ProgramData\ParentalControl\server-url.txt";
    
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
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        
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
                _logger.LogWarning("ComputerId not configured");
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
                        await _cache.SaveLastKnownLimitsAsync(record.UserId, result);
                        return result;
                    }
                }
            }
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
            var machineId = $"{hostname}-WIN";
            var osInfo = "Windows 11";
            
            var request = new RegisterComputerRequest(hostname, machineId, osInfo);
            var response = await _httpClient.PostAsJsonAsync("/api/client/register", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterComputerResponse>();
                _computerId = result!.ComputerId;
                _logger.LogInformation("Registered: {ComputerId}", result.ComputerId);
                
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
            
            var response = await _httpClient.PostAsJsonAsync("/api/client/usage", request);
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
}
