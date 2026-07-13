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

            var proxyPassPath = Path.Combine(configDir, "proxy-pass");
            if (OperatingSystem.IsLinux())
            {
                // Set the restrictive mode as part of file creation rather than writing
                // the content first and chmod-ing after -- the latter leaves the file
                // world-readable under the default umask for the window in between.
                var options = new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead
                };
                await using (var stream = new FileStream(proxyPassPath, options))
                await using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(password);
                }

                // The tray app user must be a member of the parental-control group (set up
                // by the installer). Only warn on failure -- this can legitimately run
                // before that group exists on a fresh install.
                var chgrp = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chgrp",
                    Arguments = $"parental-control {proxyPassPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                chgrp?.WaitForExit();
                if (chgrp == null || chgrp.ExitCode != 0)
                {
                    Console.WriteLine("Warning: failed to set group ownership on proxy-pass " +
                        "(the 'parental-control' group may not exist yet); the tray app may not " +
                        "be able to read the proxy password until this is retried.");
                }
            }
            else
            {
                await File.WriteAllTextAsync(proxyPassPath, password);
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
            // Deliberately no upfront "ServerUrl must be configured" check here: a missing
            // or invalid server URL must never crash the daemon (that would crash-loop the
            // systemd service and disable enforcement entirely). ServerSyncService.
            // InitializeAsync handles a missing/invalid URL by logging and retrying instead
            // of throwing -- see ParentalControlWorker, which calls it on every tick until
            // it succeeds.
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
