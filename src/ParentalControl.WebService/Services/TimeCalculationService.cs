using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Services;

public interface ITimeCalculationService
{
    Task<int> CalculateTimeRemainingAsync(Guid userId, DateOnly date);
    Task<bool> ShouldEnforceAsync(Guid userId, int timeRemaining);
    Task<bool> IsWithinAllowedHoursAsync(Guid userId, DateTime currentTime);
    Task<int> GetMinutesUntilAllowedHoursEndAsync(Guid userId, DateTime currentTime);
}

public class TimeCalculationService : ITimeCalculationService
{
    private readonly AppDbContext _context;
    private readonly IUserResolutionService _userResolution;
    
    public TimeCalculationService(AppDbContext context, IUserResolutionService userResolution)
    {
        _context = context;
        _userResolution = userResolution;
    }
    
    public async Task<int> CalculateTimeRemainingAsync(Guid userId, DateOnly date)
    {
        // Resolve to primary user for alias support
        var primaryUser = await _userResolution.ResolveToPrimaryAsync(userId);
        if (primaryUser == null) return int.MaxValue;
        
        // Inactive users have unlimited time
        if (!primaryUser.IsActive) return int.MaxValue;
        
        var profile = await _context.TimeProfiles
            .FirstOrDefaultAsync(p => p.UserId == primaryUser.Id && p.IsActive);
        
        if (profile == null) return int.MaxValue;
        
        var dayLimit = GetDailyLimit(profile, date.DayOfWeek);
        if (dayLimit == 0) return int.MaxValue;
        
        // Aggregate usage across all users in alias group
        var allUserIds = await _userResolution.GetAllUserIdsInGroupAsync(primaryUser.Id);
        
        var usedToday = await _context.TimeUsage
            .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate == date)
            .SumAsync(u => u.MinutesUsed);
        
        var adjustments = await _context.TimeAdjustments
            .Where(a => allUserIds.Contains(a.UserId) && a.AdjustmentDate == date)
            .SumAsync(a => a.MinutesAdjustment);
        
        var dailyRemaining = dayLimit - usedToday + adjustments;
        
        if (profile.WeeklyLimit > 0)
        {
            var weekStart = date.AddDays(-(int)date.DayOfWeek);
            var usedThisWeek = await _context.TimeUsage
                .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate >= weekStart && u.UsageDate < weekStart.AddDays(7))
                .SumAsync(u => u.MinutesUsed);
            
            var weeklyRemaining = profile.WeeklyLimit - usedThisWeek + adjustments;
            return Math.Min(dailyRemaining, weeklyRemaining);
        }
        
        return dailyRemaining;
    }
    
    public Task<bool> ShouldEnforceAsync(Guid userId, int timeRemaining) => Task.FromResult(timeRemaining < 0);
    
    public async Task<bool> IsWithinAllowedHoursAsync(Guid userId, DateTime currentTime)
    {
        // Resolve to primary user for alias support
        var primaryUser = await _userResolution.ResolveToPrimaryAsync(userId);
        if (primaryUser == null) return true;
        
        var profile = await _context.TimeProfiles
            .Include(p => p.AllowedHours)
            .FirstOrDefaultAsync(p => p.UserId == primaryUser.Id && p.IsActive);
        
        if (profile == null || !profile.AllowedHours.Any())
            return true; // No restrictions = always allowed
        
        var dayOfWeek = (int)currentTime.DayOfWeek;
        var currentTimeOnly = TimeOnly.FromDateTime(currentTime);
        
        var allowed = profile.AllowedHours
            .Where(ah => ah.DayOfWeek == dayOfWeek)
            .Any(ah => currentTimeOnly >= ah.StartTime && currentTimeOnly <= ah.EndTime);
        
        return allowed;
    }
    
    public async Task<int> GetMinutesUntilAllowedHoursEndAsync(Guid userId, DateTime currentTime)
    {
        // Resolve to primary user for alias support
        var primaryUser = await _userResolution.ResolveToPrimaryAsync(userId);
        if (primaryUser == null) return int.MaxValue;
        
        var profile = await _context.TimeProfiles
            .Include(p => p.AllowedHours)
            .FirstOrDefaultAsync(p => p.UserId == primaryUser.Id && p.IsActive);
        
        if (profile == null || !profile.AllowedHours.Any())
            return int.MaxValue; // No restrictions
        
        var dayOfWeek = (int)currentTime.DayOfWeek;
        var currentTimeOnly = TimeOnly.FromDateTime(currentTime);
        
        var todayHours = profile.AllowedHours
            .Where(ah => ah.DayOfWeek == dayOfWeek && currentTimeOnly >= ah.StartTime && currentTimeOnly <= ah.EndTime)
            .FirstOrDefault();
        
        if (todayHours == null)
            return 0; // Outside allowed hours
        
        var minutesUntilEnd = (int)(todayHours.EndTime.ToTimeSpan() - currentTimeOnly.ToTimeSpan()).TotalMinutes;
        return minutesUntilEnd;
    }
    
    private static int GetDailyLimit(TimeProfile profile, DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => profile.MondayLimit,
        DayOfWeek.Tuesday => profile.TuesdayLimit,
        DayOfWeek.Wednesday => profile.WednesdayLimit,
        DayOfWeek.Thursday => profile.ThursdayLimit,
        DayOfWeek.Friday => profile.FridayLimit,
        DayOfWeek.Saturday => profile.SaturdayLimit,
        DayOfWeek.Sunday => profile.SundayLimit,
        _ => 0
    };
}
