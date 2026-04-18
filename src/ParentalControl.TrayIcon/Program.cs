using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
        // Kill any existing instances before starting
        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            
            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    Console.WriteLine($"[Tray] Killing existing instance (PID: {process.Id})");
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tray] Error killing existing instances: {ex.Message}");
        }
        
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
            ToolTipText = "Loading...",
            Icon = LoadIcon()
        };

        _timer = new Timer(_ => UpdateTime(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }
    
    private Avalonia.Controls.WindowIcon LoadIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("ParentalControl.TrayIcon.icon.png");
        if (stream != null)
        {
            return new Avalonia.Controls.WindowIcon(stream);
        }
        
        // Fallback: create empty stream
        return new Avalonia.Controls.WindowIcon(new System.IO.MemoryStream());
    }

    private void LoadConfig()
    {
        try
        {
            var serverUrlPath = "/etc/parental-control/server-url";
            var computerIdPath = "/etc/parental-control/computer-id";
            var proxyUserPath = "/etc/parental-control/proxy-user";
            var proxyPassPath = "/etc/parental-control/proxy-pass";
            var configPath = "/opt/parental-control/appsettings.json";
            
            Console.WriteLine($"[Tray] Loading configuration...");
            
            if (File.Exists(serverUrlPath))
            {
                _serverUrl = File.ReadAllText(serverUrlPath).Trim();
                Console.WriteLine($"[Tray] Server URL: {_serverUrl}");
            }
            else
            {
                Console.WriteLine($"[Tray] Server URL file not found: {serverUrlPath}");
            }
            
            if (File.Exists(computerIdPath) && Guid.TryParse(File.ReadAllText(computerIdPath).Trim(), out var id))
            {
                _computerId = id;
                Console.WriteLine($"[Tray] Computer ID: {id}");
            }
            else
            {
                Console.WriteLine($"[Tray] Computer ID file not found or invalid: {computerIdPath}");
            }
            
            _username = Environment.UserName;
            Console.WriteLine($"[Tray] Username: {_username}");
            
            if (!string.IsNullOrEmpty(_serverUrl))
            {
                _httpClient = new HttpClient { BaseAddress = new Uri(_serverUrl), Timeout = TimeSpan.FromSeconds(5) };
                
                // Try persistent files first
                string? proxyUser = null;
                string? proxyPass = null;
                
                if (File.Exists(proxyUserPath))
                {
                    proxyUser = File.ReadAllText(proxyUserPath).Trim();
                    Console.WriteLine($"[Tray] Proxy user loaded from: {proxyUserPath}");
                }
                
                if (File.Exists(proxyPassPath))
                {
                    proxyPass = File.ReadAllText(proxyPassPath).Trim();
                    Console.WriteLine($"[Tray] Proxy password loaded from: {proxyPassPath}");
                }
                
                // Fall back to appsettings.json if persistent files don't exist
                if (string.IsNullOrEmpty(proxyUser) || string.IsNullOrEmpty(proxyPass))
                {
                    Console.WriteLine($"[Tray] Checking appsettings.json for proxy credentials...");
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
                                if (proxy.TryGetProperty("Username", out var user))
                                    proxyUser = user.GetString();
                                if (proxy.TryGetProperty("Password", out var pass))
                                    proxyPass = pass.GetString();
                                Console.WriteLine($"[Tray] Proxy credentials loaded from appsettings.json");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Tray] Failed to read appsettings.json: {ex.Message}");
                        }
                    }
                }
                
                // Configure basic auth if credentials are available
                if (!string.IsNullOrEmpty(proxyUser) && !string.IsNullOrEmpty(proxyPass))
                {
                    var authBytes = Encoding.ASCII.GetBytes($"{proxyUser}:{proxyPass}");
                    var authHeader = Convert.ToBase64String(authBytes);
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
                    Console.WriteLine($"[Tray] Basic authentication configured");
                }
                else
                {
                    Console.WriteLine($"[Tray] No proxy credentials found");
                }
            }
            
            Console.WriteLine($"[Tray] Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tray] Error loading config: {ex.Message}");
        }
    }

    private async void UpdateTime()
    {
        if (_httpClient == null || !_computerId.HasValue || string.IsNullOrEmpty(_username))
        {
            Console.WriteLine($"[Tray] UpdateTime: Not configured (httpClient={_httpClient != null}, computerId={_computerId.HasValue}, username={!string.IsNullOrEmpty(_username)})");
            Dispatcher.UIThread.Post(() =>
            {
                if (_trayIcon != null)
                    _trayIcon.ToolTipText = "Not configured";
            });
            return;
        }

        try
        {
            Console.WriteLine($"[Tray] Sending usage report to server...");
            Console.WriteLine($"[Tray] Auth header present: {_httpClient.DefaultRequestHeaders.Authorization != null}");
            
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
            Console.WriteLine($"[Tray] Server response: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
                if (result != null && _trayIcon != null)
                {
                    var minutes = result.TimeRemainingMinutes;
                    
                    // Update UI on UI thread
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_trayIcon != null)
                        {
                            // Check if parent account (unlimited time)
                            if (minutes >= int.MaxValue - 1000)
                            {
                                _trayIcon.ToolTipText = "Parent - No time limit";
                                Console.WriteLine($"[Tray] Parent account detected");
                            }
                            else
                            {
                                _trayIcon.ToolTipText = $"{minutes}m remaining";
                                Console.WriteLine($"[Tray] Time remaining: {minutes} minutes");
                            }
                        }
                    });
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Tray] Server error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tray] Error updating time: {ex.Message}");
            Dispatcher.UIThread.Post(() =>
            {
                if (_trayIcon != null)
                    _trayIcon.ToolTipText = "Server unavailable";
            });
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
