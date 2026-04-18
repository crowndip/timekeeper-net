using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.TrayIcon.Windows;

class Program
{
    private static NotifyIcon? _trayIcon;
    private static System.Threading.Timer? _timer;
    private static HttpClient? _httpClient;
    private static Guid? _computerId;
    private static string? _username;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        LoadConfig();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Visible = true,
            Text = "Loading..."
        };

        _timer = new System.Threading.Timer(_ => UpdateTime(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        Application.Run();
    }

    private static void LoadConfig()
    {
        try
        {
            var serverUrlPath = @"C:\ProgramData\ParentalControl\server-url.txt";
            var computerIdPath = @"C:\ProgramData\ParentalControl\computer-id.txt";
            var proxyUserPath = @"C:\ProgramData\ParentalControl\proxy-user.txt";
            var proxyPassPath = @"C:\ProgramData\ParentalControl\proxy-pass.txt";
            var configPath = @"C:\ProgramData\ParentalControl\appsettings.json";

            if (File.Exists(serverUrlPath))
            {
                var serverUrl = File.ReadAllText(serverUrlPath).Trim();
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    _httpClient = new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(5) };
                    
                    // Try persistent files first
                    string? proxyUser = null;
                    string? proxyPass = null;
                    
                    if (File.Exists(proxyUserPath))
                        proxyUser = File.ReadAllText(proxyUserPath).Trim();
                    
                    if (File.Exists(proxyPassPath))
                        proxyPass = File.ReadAllText(proxyPassPath).Trim();
                    
                    // Fall back to appsettings.json if persistent files don't exist
                    if (string.IsNullOrEmpty(proxyUser) || string.IsNullOrEmpty(proxyPass))
                    {
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
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Configure basic auth if credentials are available
                    if (!string.IsNullOrEmpty(proxyUser) && !string.IsNullOrEmpty(proxyPass))
                    {
                        var authBytes = Encoding.ASCII.GetBytes($"{proxyUser}:{proxyPass}");
                        var authHeader = Convert.ToBase64String(authBytes);
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
                    }
                }
            }

            if (File.Exists(computerIdPath) && Guid.TryParse(File.ReadAllText(computerIdPath).Trim(), out var id))
                _computerId = id;

            _username = Environment.UserName;
        }
        catch { }
    }

    private static async void UpdateTime()
    {
        if (_httpClient == null || !_computerId.HasValue || string.IsNullOrEmpty(_username))
        {
            if (_trayIcon != null)
                _trayIcon.Text = "Not configured";
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
                    
                    // Check if parent account (unlimited time)
                    if (minutes >= int.MaxValue - 1000)
                    {
                        _trayIcon.Text = "P - Parent (No limit)";
                    }
                    else
                    {
                        _trayIcon.Text = $"{minutes}m remaining";
                    }
                }
            }
        }
        catch
        {
            if (_trayIcon != null)
                _trayIcon.Text = "Server unavailable";
        }
    }
}
