using Microsoft.AspNetCore.Mvc;
using ParentalControl.WebService.Services;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (_authService.ValidatePassword(request.Password))
        {
            _authService.SetAuthenticated();
            return Ok(new { success = true });
        }

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
