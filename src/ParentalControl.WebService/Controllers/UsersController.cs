using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.Shared.DTOs;
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
    private readonly IClockService _clock;

    public UsersController(AppDbContext context, IUserResolutionService userResolution, IClockService clock)
    {
        _context = context;
        _userResolution = userResolution;
        _clock = clock;
    }

    // Full user records (including Email) aren't needed by any anonymous flow -- unlike
    // time-status below, which is deliberately anonymous so a child can check their own
    // remaining time without a parent password. Nothing in this repo's UI calls these
    // two REST endpoints (the Blazor page queries AppDbContext directly server-side).
    [HttpGet]
    [RequireAuth]
    public async Task<IActionResult> GetUsers([FromQuery] AccountType? accountType = null)
    {
        var query = _context.Users.AsQueryable();

        if (accountType.HasValue)
            query = query.Where(u => u.AccountType == accountType.Value);

        var users = await query.OrderBy(u => u.Username).ToListAsync();
        return Ok(new { success = true, data = users });
    }

    [HttpGet("{id}")]
    [RequireAuth]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });

        return Ok(new { success = true, data = user });
    }

    [HttpPost]
    [RequireAuth]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ValidationHelper.ValidateUsername(request.Username))
            return BadRequest(new { success = false, error = "Invalid username. Use only letters, numbers, _, -, . (max 64 chars)" });

        if (!ValidationHelper.ValidateEmail(request.Email))
            return BadRequest(new { success = false, error = "Invalid email format" });

        if (!Enum.TryParse<AccountType>(request.AccountType, ignoreCase: true, out var accountType))
            return BadRequest(new { success = false, error = "Invalid account type" });

        // Normalize the same way the client-reporting path does (ClientController.EnsureUserExistsAsync)
        // so a user created here as "Alice" is the same row a client reporting "alice" resolves to.
        var username = ValidationHelper.NormalizeUsername(request.Username);

        if (await _context.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { success = false, error = "Username already exists" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = request.FullName,
            Email = request.Email,
            AccountType = accountType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, data = user, message = "User created successfully" });
    }

    [HttpPut("{id}")]
    [RequireAuth]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        if (!ValidationHelper.ValidateEmail(request.Email))
            return BadRequest(new { success = false, error = "Invalid email format" });

        if (!Enum.TryParse<AccountType>(request.AccountType, ignoreCase: true, out var accountType))
            return BadRequest(new { success = false, error = "Invalid account type" });

        var existing = await _context.Users.FindAsync(id);
        if (existing == null)
            return NotFound(new { success = false, error = "User not found" });

        existing.FullName = request.FullName;
        existing.Email = request.Email;
        existing.AccountType = accountType;
        existing.IsActive = request.IsActive;
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
            AdjustmentDate = _clock.LocalToday(),
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
        
        var today = _clock.LocalToday();
        var timeCalc = HttpContext.RequestServices.GetRequiredService<ITimeCalculationService>();
        var timeRemaining = await timeCalc.CalculateTimeRemainingAsync(id, today);

        // timeRemaining above resolves the alias group internally; usedToday must be
        // computed over that same group, or the two numbers in this response can
        // disagree for a user who has aliases (or is one).
        var primaryUser = await _userResolution.ResolveToPrimaryAsync(id);
        var allUserIds = primaryUser != null
            ? await _userResolution.GetAllUserIdsInGroupAsync(primaryUser.Id)
            : new List<Guid> { id };

        var usedToday = await _context.TimeUsage
            .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate == today)
            .SumAsync(u => u.MinutesUsed);

        var adjustmentsToday = await _context.TimeAdjustments
            .Where(a => allUserIds.Contains(a.UserId) && a.AdjustmentDate == today)
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
    [RequireAuth]
    public async Task<IActionResult> GetAliases(Guid primaryUserId)
    {
        var aliases = await _context.Users
            .Where(u => u.PrimaryUserId == primaryUserId)
            .OrderBy(u => u.Username)
            .ToListAsync();

        return Ok(new { success = true, data = aliases });
    }

    [HttpGet("{userId}/usage-breakdown")]
    [RequireAuth]
    public async Task<IActionResult> GetUsageBreakdown(Guid userId, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate)
    {
        var end = endDate ?? _clock.LocalToday();
        var start = startDate ?? end.AddDays(-30);

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
