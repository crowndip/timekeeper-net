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
    private readonly IUserResolutionService _userResolution;

    public UsersController(AppDbContext context, IUserResolutionService userResolution)
    {
        _context = context;
        _userResolution = userResolution;
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

    [HttpPost("{primaryUserId}/aliases/{aliasUserId}")]
    [RequireAuth]
    public async Task<IActionResult> AddAlias(Guid primaryUserId, Guid aliasUserId)
    {
        var primaryUser = await _context.Users.Include(u => u.Aliases).FirstOrDefaultAsync(u => u.Id == primaryUserId);
        if (primaryUser == null)
            return NotFound(new { success = false, error = "Primary user not found" });

        if (primaryUser.PrimaryUserId.HasValue)
            return BadRequest(new { success = false, error = "Cannot make an alias the primary user" });

        var aliasUser = await _context.Users.Include(u => u.Aliases).Include(u => u.TimeProfiles).FirstOrDefaultAsync(u => u.Id == aliasUserId);
        if (aliasUser == null)
            return NotFound(new { success = false, error = "Alias user not found" });

        if (!await _userResolution.CanBecomeAliasAsync(aliasUserId))
            return BadRequest(new { success = false, error = "User cannot become an alias (has aliases or active profile)" });

        if (aliasUser.PrimaryUserId.HasValue)
            return BadRequest(new { success = false, error = "User is already an alias" });

        aliasUser.PrimaryUserId = primaryUserId;
        aliasUser.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = $"{aliasUser.Username} is now an alias of {primaryUser.Username}" });
    }

    [HttpDelete("{aliasUserId}/unlink")]
    [RequireAuth]
    public async Task<IActionResult> RemoveAlias(Guid aliasUserId)
    {
        var aliasUser = await _context.Users.FindAsync(aliasUserId);
        if (aliasUser == null)
            return NotFound(new { success = false, error = "User not found" });

        if (!aliasUser.PrimaryUserId.HasValue)
            return BadRequest(new { success = false, error = "User is not an alias" });

        aliasUser.PrimaryUserId = null;
        aliasUser.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = $"{aliasUser.Username} is no longer an alias" });
    }

    [HttpGet("{primaryUserId}/aliases")]
    public async Task<IActionResult> GetAliases(Guid primaryUserId)
    {
        var aliases = await _context.Users
            .Where(u => u.PrimaryUserId == primaryUserId)
            .OrderBy(u => u.Username)
            .ToListAsync();

        return Ok(new { success = true, data = aliases });
    }

    [HttpGet("{userId}/usage-breakdown")]
    public async Task<IActionResult> GetUsageBreakdown(Guid userId, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var primaryUser = await _userResolution.ResolveToPrimaryAsync(userId);
        if (primaryUser == null)
            return NotFound(new { success = false, error = "User not found" });

        var allUserIds = await _userResolution.GetAllUserIdsInGroupAsync(primaryUser.Id);

        var usage = await _context.TimeUsage
            .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate >= start && u.UsageDate <= end)
            .Include(u => u.User)
            .GroupBy(u => new { u.UserId, u.User.Username, u.UsageDate })
            .Select(g => new { g.Key.UserId, g.Key.Username, g.Key.UsageDate, MinutesUsed = g.Sum(u => u.MinutesUsed) })
            .OrderBy(u => u.UsageDate)
            .ToListAsync();

        var totalMinutes = usage.Sum(u => u.MinutesUsed);
        var byUser = usage.GroupBy(u => new { u.UserId, u.Username })
            .Select(g => new { g.Key.UserId, g.Key.Username, TotalMinutes = g.Sum(u => u.MinutesUsed) })
            .ToList();

        var dailyBreakdown = usage.GroupBy(u => u.UsageDate)
            .Select(g => new
            {
                Date = g.Key,
                TotalMinutes = g.Sum(u => u.MinutesUsed),
                ByUser = g.ToDictionary(u => u.Username, u => u.MinutesUsed)
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                primaryUser = new { primaryUser.Id, primaryUser.Username, TotalMinutes = totalMinutes },
                byUser,
                dailyBreakdown
            }
        });
    }
}
