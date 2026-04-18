using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using ParentalControl.WebService.Filters;

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
    [RequireAuth]
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
    [RequireAuth]
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
    [RequireAuth]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, message = "User deleted successfully" });
    }
    
    [HttpPost("{id}/adjust-time")]
    [RequireAuth]
    public async Task<IActionResult> AdjustTime(Guid id, [FromBody] TimeAdjustmentRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });
        
        if (request.MinutesAdjustment < -1440 || request.MinutesAdjustment > 1440)
            return BadRequest(new { success = false, error = "Adjustment must be between -1440 and 1440 minutes" });
        
        var adjustment = new TimeAdjustment
        {
            UserId = id,
            AdjustmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MinutesAdjustment = request.MinutesAdjustment,
            Reason = request.Reason ?? "Manual adjustment",
            CreatedBy = "Admin"
        };
        
        _context.TimeAdjustments.Add(adjustment);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, message = $"Added {request.MinutesAdjustment} minutes for today", data = adjustment });
    }
    
    [HttpGet("{id}/time-status")]
    public async Task<IActionResult> GetTimeStatus(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var timeCalc = HttpContext.RequestServices.GetRequiredService<ITimeCalculationService>();
        var timeRemaining = await timeCalc.CalculateTimeRemainingAsync(id, today);
        
        var usedToday = await _context.TimeUsage
            .Where(u => u.UserId == id && u.UsageDate == today)
            .SumAsync(u => u.MinutesUsed);
        
        var adjustmentsToday = await _context.TimeAdjustments
            .Where(a => a.UserId == id && a.AdjustmentDate == today)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.MinutesAdjustment, a.Reason, a.CreatedAt })
            .ToListAsync();
        
        return Ok(new { 
            success = true, 
            data = new { 
                timeRemaining, 
                usedToday, 
                adjustmentsToday,
                totalAdjustments = adjustmentsToday.Sum(a => a.MinutesAdjustment)
            } 
        });
    }

    [HttpPatch("{id}/active")]
    [RequireAuth]
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
