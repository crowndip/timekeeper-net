using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParentalControl.Client.Services;

namespace ParentalControl.Client;

public class ParentalControlWorker : BackgroundService
{
    private readonly ISessionMonitor _sessionMonitor;
    private readonly ITimeTracker _timeTracker;
    private readonly IServerSyncService _serverSync;
    private readonly IEnforcementEngine _enforcement;
    private readonly ILogger<ParentalControlWorker> _logger;
    private readonly int _tickIntervalSeconds;
    
    public ParentalControlWorker(
        ISessionMonitor sessionMonitor,
        ITimeTracker timeTracker,
        IServerSyncService serverSync,
        IEnforcementEngine enforcement,
        ILogger<ParentalControlWorker> logger,
        IConfiguration configuration)
    {
        _sessionMonitor = sessionMonitor;
        _timeTracker = timeTracker;
        _serverSync = serverSync;
        _enforcement = enforcement;
        _logger = logger;
        _tickIntervalSeconds = configuration.GetValue<int>("ParentalControl:TickIntervalSeconds", 60);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parental Control Client started, tick interval: {Interval}s", _tickIntervalSeconds);
        
        // Wait for system to stabilize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tick");
            }
            
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_tickIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
        
        _logger.LogInformation("Parental Control Client stopping");
    }
    
    private async Task ProcessTickAsync()
    {
        var sessions = await _sessionMonitor.GetActiveSessionsAsync();
        
        foreach (var session in sessions)
        {
            await _timeTracker.RecordMinuteAsync(session);
        }
        
        var usageData = await _timeTracker.GetPendingUsageAsync();
        if (usageData.Count > 0)
        {
            var response = await _serverSync.SubmitUsageAsync(usageData);
            if (response != null)
            {
                // Server available - use server response
                await _enforcement.CheckAndEnforceAsync(response);
                await _timeTracker.MarkAsSyncedAsync(usageData.Select(u => u.Id).ToList());
            }
            else
            {
                // Server unavailable - use offline mode
                _logger.LogWarning("Server unavailable, using offline mode");
                await _enforcement.CheckAndEnforceOfflineAsync(usageData);
            }
        }
    }
}
