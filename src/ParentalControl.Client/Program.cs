using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParentalControl.Client;
using ParentalControl.Client.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("/var/log/parental-control/client.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting Parental Control Client");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .UseSystemd()
        .ConfigureServices((context, services) =>
        {
            var config = context.Configuration.GetSection("ParentalControl");
            
            if (string.IsNullOrEmpty(config["ServerUrl"]))
                throw new InvalidOperationException("ServerUrl is not configured");

            services.AddHttpClient<IServerSyncService, ServerSyncService>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            
            services.AddSingleton<ISessionMonitor, SystemdSessionMonitor>();
            services.AddSingleton<ITimeTracker, TimeTracker>();
            services.AddSingleton<IEnforcementEngine, EnforcementEngine>();
            services.AddSingleton<ILocalCache, LocalCache>();
            services.AddHostedService<ParentalControlWorker>();
        })
        .Build();
    
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("Parental Control Client stopped");
    await Log.CloseAndFlushAsync();
}
