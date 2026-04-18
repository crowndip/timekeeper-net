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

    // Handle command-line settings
    if (args.Length > 0 && args[0] == "set")
    {
        var configDir = "/etc/parental-control";
        Directory.CreateDirectory(configDir);
        
        if (args.Length >= 3 && args[1] == "server-url")
        {
            var serverUrl = args[2];
            await File.WriteAllTextAsync(Path.Combine(configDir, "server-url"), serverUrl);
            Console.WriteLine($"Server URL set to: {serverUrl}");
            return 0;
        }
        else if (args.Length >= 4 && args[1] == "proxy")
        {
            var username = args[2];
            var password = args[3];
            await File.WriteAllTextAsync(Path.Combine(configDir, "proxy-user"), username);
            await File.WriteAllTextAsync(Path.Combine(configDir, "proxy-pass"), password);
            
            // Set permissions - readable by all (needed for tray app running as user)
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(Path.Combine(configDir, "proxy-pass"), 
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
            
            Console.WriteLine($"Proxy credentials set for user: {username}");
            return 0;
        }
        else
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  ParentalControl.Client set server-url <url>");
            Console.WriteLine("  ParentalControl.Client set proxy <username> <password>");
            return 1;
        }
    }

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
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.Information("Parental Control Client stopped");
    await Log.CloseAndFlushAsync();
}
