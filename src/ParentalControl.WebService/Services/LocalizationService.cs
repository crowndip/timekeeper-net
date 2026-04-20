using System.Text.Json;

namespace ParentalControl.WebService.Services;

public class LocalizationService
{
    private readonly IWebHostEnvironment _env;
    private readonly Dictionary<string, JsonElement> _translations = new();
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
        
        if (!Directory.Exists(localizationPath))
        {
            _logger.LogWarning("Localization directory not found: {Path}", localizationPath);
            return;
        }

        foreach (var file in Directory.GetFiles(localizationPath, "*.json"))
        {
            try
            {
                var culture = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json);
                _translations[culture] = doc.RootElement.Clone();
                _logger.LogInformation("Loaded translations for culture: {Culture}", culture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load translation file: {File}", file);
            }
        }
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
}
