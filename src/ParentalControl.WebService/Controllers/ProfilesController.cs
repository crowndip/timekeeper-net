using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProfilesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfiles([FromQuery] Guid? userId = null)
    {
        var query = _context.TimeProfiles.Include(p => p.AllowedHours).AsQueryable();
        
        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);
        
        var profiles = await query.OrderBy(p => p.Name).ToListAsync();
        return Ok(new { success = true, data = profiles });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var profile = await _context.TimeProfiles
            .Include(p => p.AllowedHours)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (profile == null)
            return NotFound(new { success = false, error = "Profile not found" });
        
        return Ok(new { success = true, data = profile });
    }

    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] TimeProfile profile)
    {
        if (!ValidationHelper.ValidateProfileName(profile.Name))
            return BadRequest(new { success = false, error = "Invalid profile name (max 100 chars)" });
        
        if (!ValidationHelper.ValidateTimeLimit(profile.MondayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.TuesdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.WednesdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.ThursdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.FridayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.SaturdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.SundayLimit))
            return BadRequest(new { success = false, error = "Time limits must be between 0 and 1440 minutes" });
        
        if (await _context.TimeProfiles.AnyAsync(p => p.UserId == profile.UserId && p.Name == profile.Name))
            return BadRequest(new { success = false, error = "Profile name already exists for this user" });

        profile.Id = Guid.NewGuid();
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        
        if (profile.IsActive)
        {
            var otherProfiles = await _context.TimeProfiles
                .Where(p => p.UserId == profile.UserId && p.IsActive)
                .ToListAsync();
            otherProfiles.ForEach(p => p.IsActive = false);
        }
        
        _context.TimeProfiles.Add(profile);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = profile, message = "Profile created successfully" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] TimeProfile profile)
    {
        if (!ValidationHelper.ValidateProfileName(profile.Name))
            return BadRequest(new { success = false, error = "Invalid profile name (max 100 chars)" });
        
        if (!ValidationHelper.ValidateTimeLimit(profile.MondayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.TuesdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.WednesdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.ThursdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.FridayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.SaturdayLimit) ||
            !ValidationHelper.ValidateTimeLimit(profile.SundayLimit))
            return BadRequest(new { success = false, error = "Time limits must be between 0 and 1440 minutes" });
        
        var existing = await _context.TimeProfiles.FindAsync(id);
        if (existing == null)
            return NotFound(new { success = false, error = "Profile not found" });

        existing.Name = profile.Name;
        existing.IsActive = profile.IsActive;
        existing.MondayLimit = profile.MondayLimit;
        existing.TuesdayLimit = profile.TuesdayLimit;
        existing.WednesdayLimit = profile.WednesdayLimit;
        existing.ThursdayLimit = profile.ThursdayLimit;
        existing.FridayLimit = profile.FridayLimit;
        existing.SaturdayLimit = profile.SaturdayLimit;
        existing.SundayLimit = profile.SundayLimit;
        existing.UpdatedAt = DateTime.UtcNow;
        
        if (profile.IsActive)
        {
            var otherProfiles = await _context.TimeProfiles
                .Where(p => p.UserId == existing.UserId && p.Id != id && p.IsActive)
                .ToListAsync();
            otherProfiles.ForEach(p => p.IsActive = false);
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = existing, message = "Profile updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id)
    {
        var profile = await _context.TimeProfiles.FindAsync(id);
        if (profile == null)
            return NotFound(new { success = false, error = "Profile not found" });

        _context.TimeProfiles.Remove(profile);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, message = "Profile deleted successfully" });
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> ActivateProfile(Guid id)
    {
        var profile = await _context.TimeProfiles.FindAsync(id);
        if (profile == null)
            return NotFound(new { success = false, error = "Profile not found" });

        var otherProfiles = await _context.TimeProfiles
            .Where(p => p.UserId == profile.UserId && p.Id != id && p.IsActive)
            .ToListAsync();
        otherProfiles.ForEach(p => p.IsActive = false);
        
        profile.IsActive = true;
        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, data = profile, message = "Profile activated successfully" });
    }
}
