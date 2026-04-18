using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.TrayIcon;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TrayApp>()
            .UsePlatformDetect()
            .LogToTrace();
}

public class TrayApp : Application
{
    private Avalonia.Controls.TrayIcon? _trayIcon;
    private Timer? _timer;
    private HttpClient? _httpClient;
    private string? _serverUrl;
    private Guid? _computerId;
    private string? _username;

    public override void Initialize()
    {
        LoadConfig();
        
        _trayIcon = new Avalonia.Controls.TrayIcon
        {
            IsVisible = true,
            ToolTipText = "Loading..."
        };

        _timer = new Timer(_ => UpdateTime(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private void LoadConfig()
    {
        try
        {
            var serverUrlPath = "/etc/parental-control/server-url";
            var computerIdPath = "/etc/parental-control/computer-id";
            var configPath = "/opt/parental-control/appsettings.json";
            
            if (File.Exists(serverUrlPath))
                _serverUrl = File.ReadAllText(serverUrlPath).Trim();
            
            if (File.Exists(computerIdPath) && Guid.TryParse(File.ReadAllText(computerIdPath).Trim(), out var id))
                _computerId = id;
            
            _username = Environment.UserName;
            
            if (!string.IsNullOrEmpty(_serverUrl))
            {
                _httpClient = new HttpClient { BaseAddress = new Uri(_serverUrl), Timeout = TimeSpan.FromSeconds(5) };
                
                // Check for basic auth config
                if (File.Exists(configPath))
                {
                    try
                    {
                        var json = File.ReadAllText(configPath);
                        var config = JsonSerializer.Deserialize<JsonElement>(json);
                        
                        if (config.TryGetProperty("ParentalControl", out var pc) &&
                            pc.TryGetProperty("ReverseProxy", out var proxy) &&
                            proxy.TryGetProperty("Enabled", out var enabled) && enabled.GetBoolean())
                        {
                            if (proxy.TryGetProperty("Username", out var user) &&
                                proxy.TryGetProperty("Password", out var pass))
                            {
                                var authBytes = Encoding.ASCII.GetBytes($"{user.GetString()}:{pass.GetString()}");
                                var authHeader = Convert.ToBase64String(authBytes);
                                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private async void UpdateTime()
    {
        if (_httpClient == null || !_computerId.HasValue || string.IsNullOrEmpty(_username))
        {
            if (_trayIcon != null)
                _trayIcon.ToolTipText = "Not configured";
            return;
        }

        try
        {
            var request = new UsageReportRequest(
                _computerId.Value,
                Guid.Empty,
                _username,
                null,
                DateTime.UtcNow,
                0,
                0,
                true
            );

            var response = await _httpClient.PostAsJsonAsync("/api/client/usage", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
                if (result != null && _trayIcon != null)
                {
                    var minutes = result.TimeRemainingMinutes;
                    _trayIcon.ToolTipText = $"{minutes}m remaining";
                }
            }
        }
        catch
        {
            if (_trayIcon != null)
                _trayIcon.ToolTipText = "Server unavailable";
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
