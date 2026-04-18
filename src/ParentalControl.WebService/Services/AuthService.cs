using Microsoft.AspNetCore.Http;

namespace ParentalControl.WebService.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string AuthSessionKey = "IsAuthenticated";

    public AuthService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool ValidatePassword(string password)
    {
        var configPassword = _configuration["LimitAdministratorPassword"];
        return !string.IsNullOrEmpty(password) && password == configPassword;
    }

    public void SetAuthenticated()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString(AuthSessionKey, "true");
        }
    }

    public bool IsAuthenticated()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString(AuthSessionKey) == "true";
    }

    public void ClearAuthentication()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.Clear();
    }
}
