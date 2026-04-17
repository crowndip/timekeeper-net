using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using System.Security.Cryptography;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComputersController : ControllerBase
{
    private readonly AppDbContext _context;

    public ComputersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetComputers()
    {
        var computers = await _context.Computers
            .OrderByDescending(c => c.LastSeenAt)
            .ToListAsync();
        
        return Ok(new { success = true, data = computers });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetComputer(Guid id)
    {
        var computer = await _context.Computers.FindAsync(id);
        if (computer == null)
            return NotFound(new { success = false, error = "Computer not found" });
        
        return Ok(new { success = true, data = computer });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteComputer(Guid id)
    {
        var computer = await _context.Computers.FindAsync(id);
        if (computer == null)
            return NotFound(new { success = false, error = "Computer not found" });

        _context.Computers.Remove(computer);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, message = "Computer deleted successfully" });
    }

    [HttpPatch("{id}/active")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var computer = await _context.Computers.FindAsync(id);
        if (computer == null)
            return NotFound(new { success = false, error = "Computer not found" });

        computer.IsActive = !computer.IsActive;
        computer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = computer, message = $"Computer {(computer.IsActive ? "activated" : "deactivated")} successfully" });
    }

    [HttpPost("{id}/regenerate-key")]
    public async Task<IActionResult> RegenerateApiKey(Guid id)
    {
        var computer = await _context.Computers.FindAsync(id);
        if (computer == null)
            return NotFound(new { success = false, error = "Computer not found" });

        computer.ApiKey = GenerateApiKey();
        computer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = new { apiKey = computer.ApiKey }, message = "API key regenerated successfully" });
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32];
    }
}
