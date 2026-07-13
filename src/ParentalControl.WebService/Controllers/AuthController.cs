using Microsoft.AspNetCore.Mvc;
using ParentalControl.WebService.Services;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (_authService.IsLockedOut(ipAddress, out var retryAfter))
        {
            _logger.LogWarning("Login attempt from locked-out IP {IpAddress}, retry after {RetryAfter}s", ipAddress, (int)retryAfter.TotalSeconds);
            Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { success = false, error = $"Too many failed attempts. Try again in {(int)retryAfter.TotalSeconds}s." });
        }

        if (_authService.ValidatePassword(request.Password))
        {
            _authService.RecordLoginSuccess(ipAddress);
            _authService.SetAuthenticated();
            return Ok(new { success = true });
        }

        _authService.RecordLoginFailure(ipAddress);
        _logger.LogWarning("Failed login attempt from {IpAddress}", ipAddress);
        return Unauthorized(new { success = false, error = "Invalid password" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _authService.ClearAuthentication();
        return Ok(new { success = true });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { authenticated = _authService.IsAuthenticated() });
    }
}

public record LoginRequest(string Password);
