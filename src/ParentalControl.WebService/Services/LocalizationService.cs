using System.Text.Json;

namespace ParentalControl.WebService.Services;

public class LocalizationService
{
    private readonly IWebHostEnvironment _env;
    private readonly Dictionary<string, JsonElement> _translations = new();
    private readonly Dictionary<string, string> _languageNames = new()
    {
        { "en", "🇬🇧 English" },
        { "cs", "🇨🇿 Čeština" },
        { "de", "🇩🇪 Deutsch" },
        { "es", "🇪🇸 Español" },
        { "fr", "🇫🇷 Français" },
        { "pl", "🇵🇱 Polski" },
        { "sk", "🇸🇰 Slovenčina" },
        { "ru", "🇷🇺 Русский" }
    };
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(IWebHostEnvironment env, ILogger<LocalizationService> logger)
    {
        _env = env;
        _logger = logger;
        LoadTranslations();
    }

    private void LoadTranslations()
    {
        var localizationPath = Path.Combine(_env.WebRootPath, "localization");
        
        _logger.LogInformation("WebRootPath: {WebRootPath}", _env.WebRootPath);
        _logger.LogInformation("Localization path: {LocalizationPath}", localizationPath);
        _logger.LogInformation("Directory exists: {Exists}", Directory.Exists(localizationPath));
        
        if (!Directory.Exists(localizationPath))
        {
            _logger.LogWarning("Localization directory not found: {Path}", localizationPath);
            // Try to list what's in wwwroot
            if (Directory.Exists(_env.WebRootPath))
            {
                var dirs = Directory.GetDirectories(_env.WebRootPath);
                _logger.LogWarning("Directories in wwwroot: {Dirs}", string.Join(", ", dirs.Select(Path.GetFileName)));
            }
            return;
        }

        var files = Directory.GetFiles(localizationPath, "*.json");
        _logger.LogInformation("Found {Count} JSON files in localization directory", files.Length);
        
        foreach (var file in files)
        {
            try
            {
                var culture = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json);
                _translations[culture] = doc.RootElement.Clone();
                _logger.LogInformation("Loaded translations for culture: {Culture} from {File}", culture, file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load translation file: {File}", file);
            }
        }
        
        _logger.LogInformation("Total translations loaded: {Count}", _translations.Count);
    }

    public string Get(string culture, string key)
    {
        if (!_translations.TryGetValue(culture, out var cultureTrans))
        {
            // Fallback to English
            if (!_translations.TryGetValue("en", out cultureTrans))
            {
                return key; // No translations available
            }
        }

        return GetNestedValue(cultureTrans, key) ?? key;
    }

    public string Format(string culture, string key, params object[] args)
    {
        var template = Get(culture, key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    private string? GetNestedValue(JsonElement element, string key)
    {
        var parts = key.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(part, out var next))
                return null;

            current = next;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    public IEnumerable<string> GetAvailableCultures()
    {
        return _translations.Keys;
    }

    public Dictionary<string, string> GetAvailableLanguages()
    {
        var result = new Dictionary<string, string>();
        foreach (var culture in _translations.Keys.OrderBy(c => c))
        {
            result[culture] = _languageNames.TryGetValue(culture, out var name) ? name : culture.ToUpper();
        }
        return result;
    }
}
