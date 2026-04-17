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
}

public class ServerSyncService : IServerSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerSyncService> _logger;
    private readonly ILocalCache _cache;
    
    public ServerSyncService(HttpClient httpClient, IConfiguration configuration, ILogger<ServerSyncService> logger, ILocalCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        
        var serverUrl = configuration["ParentalControl:ServerUrl"];
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
            var computerIdStr = _configuration["ParentalControl:ComputerId"];
            if (string.IsNullOrEmpty(computerIdStr) || !Guid.TryParse(computerIdStr, out var computerId))
            {
                _logger.LogWarning("ComputerId not configured, skipping usage submission");
                return null;
            }
            
            foreach (var record in records)
            {
                var request = new UsageReportRequest(
                    computerId,
                    record.UserId,
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
                _logger.LogInformation("Registered with server: {ComputerId}", result!.ComputerId);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register");
        }
        
        return false;
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
