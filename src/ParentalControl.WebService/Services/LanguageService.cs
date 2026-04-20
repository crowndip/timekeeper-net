using Microsoft.JSInterop;

namespace ParentalControl.WebService.Services;

public class LanguageService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string LanguageCookieName = "language";

    public LanguageService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentLanguage()
    {
        // Try to get from cookie first
        var cookie = _httpContextAccessor.HttpContext?.Request.Cookies[LanguageCookieName];
        if (!string.IsNullOrEmpty(cookie))
            return cookie;

        // Try to get from Accept-Language header
        var acceptLanguage = _httpContextAccessor.HttpContext?.Request.Headers["Accept-Language"].ToString();
        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            return MapBrowserLanguage(acceptLanguage);
        }

        return "en"; // Default
    }

    public void SetLanguage(string culture)
    {
        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false, // Allow JavaScript access
            SameSite = SameSiteMode.Lax
        };

        _httpContextAccessor.HttpContext?.Response.Cookies.Append(LanguageCookieName, culture, cookieOptions);
    }

    private string MapBrowserLanguage(string acceptLanguage)
    {
        // Accept-Language format: "cs-CZ,cs;q=0.9,en-US;q=0.8,en;q=0.7"
        var languages = acceptLanguage.Split(',')
            .Select(l => l.Split(';')[0].Trim())
            .ToList();

        foreach (var lang in languages)
        {
            if (lang.StartsWith("cs", StringComparison.OrdinalIgnoreCase))
                return "cs";
            if (lang.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                return "de";
        }

        return "en"; // Default
    }
}
