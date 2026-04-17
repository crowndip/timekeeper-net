using ParentalControl.Client.Windows;
using ParentalControl.Client.Windows.Services;

// Handle command-line settings
if (args.Length > 0 && args[0] == "set")
{
    if (args.Length < 3 || args[1] != "server-url")
    {
        Console.WriteLine("Usage: ParentalControl.Client.Windows.exe set server-url <url>");
        return 1;
    }
    
    var serverUrl = args[2];
    var configDir = @"C:\ProgramData\ParentalControl";
    Directory.CreateDirectory(configDir);
    await File.WriteAllTextAsync(Path.Combine(configDir, "server-url.txt"), serverUrl);
    Console.WriteLine($"Server URL set to: {serverUrl}");
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ParentalControlClient";
});

builder.Services.AddHttpClient<IServerSyncService, ServerSyncService>();
builder.Services.AddSingleton<ILocalCache, LocalCache>();
builder.Services.AddSingleton<ISessionMonitor, WindowsSessionMonitor>();
builder.Services.AddSingleton<IEnforcementEngine, WindowsEnforcementEngine>();
builder.Services.AddHostedService<ParentalControlWorker>();

var host = builder.Build();
host.Run();
return 0;
