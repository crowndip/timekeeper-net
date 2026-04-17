using ParentalControl.Client.Windows;
using ParentalControl.Client.Windows.Services;

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
