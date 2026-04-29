using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParentalControl.Client;
using ParentalControl.Client.Services;
using Serilog;
using Serilog.Events;

// Ensure the log directory exists before Serilog tries to open the file.
Directory.CreateDirectory("/var/log/parental-control");

Log.Logger = new LoggerConfiguration()
    // Show everything from our own code; suppress noisy framework namespaces.
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .WriteTo.File(
        path: "/var/log/parental-control/client-.log",
        rollingInterval: RollingInterval.Day,
        // Roll within a day once a file reaches 10 MB (creates client-20260429_001.log etc.)
        fileSizeLimitBytes: 10L * 1024 * 1024,
        rollOnFileSizeLimit: true,
        // Keep at most 14 files total (covers ~14 days of normal use)
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
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
            
            // Set permissions: readable by root (owner) and parental-control group only.
            // The tray app user must be a member of the parental-control group (set up by the installer).
            if (OperatingSystem.IsLinux())
            {
                var proxyPassPath = Path.Combine(configDir, "proxy-pass");
                File.SetUnixFileMode(proxyPassPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chgrp",
                    Arguments = $"parental-control {proxyPassPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                })?.WaitForExit();
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
