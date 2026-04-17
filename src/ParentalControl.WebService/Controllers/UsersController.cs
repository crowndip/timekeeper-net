using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] AccountType? accountType = null)
    {
        var query = _context.Users.AsQueryable();
        
        if (accountType.HasValue)
            query = query.Where(u => u.AccountType == accountType.Value);
        
        var users = await query.OrderBy(u => u.Username).ToListAsync();
        return Ok(new { success = true, data = users });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });
        
        return Ok(new { success = true, data = user });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] User user)
    {
        if (!ValidationHelper.ValidateUsername(user.Username))
            return BadRequest(new { success = false, error = "Invalid username. Use only letters, numbers, _, -, . (max 64 chars)" });
        
        if (!ValidationHelper.ValidateEmail(user.Email))
            return BadRequest(new { success = false, error = "Invalid email format" });
        
        if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            return BadRequest(new { success = false, error = "Username already exists" });

        user.Id = Guid.NewGuid();
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = user, message = "User created successfully" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] User user)
    {
        if (!ValidationHelper.ValidateEmail(user.Email))
            return BadRequest(new { success = false, error = "Invalid email format" });
        
        var existing = await _context.Users.FindAsync(id);
        if (existing == null)
            return NotFound(new { success = false, error = "User not found" });

        existing.FullName = user.FullName;
        existing.Email = user.Email;
        existing.AccountType = user.AccountType;
        existing.IsActive = user.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = existing, message = "User updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, message = "User deleted successfully" });
    }

    [HttpPatch("{id}/active")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = user, message = $"User {(user.IsActive ? "activated" : "deactivated")} successfully" });
    }
}
