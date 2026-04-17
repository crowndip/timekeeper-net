using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Services;

public interface ITimeCalculationService
{
    Task<int> CalculateTimeRemainingAsync(Guid userId, DateOnly date);
    Task<bool> ShouldEnforceAsync(Guid userId, int timeRemaining);
    Task<bool> IsWithinAllowedHoursAsync(Guid userId, DateTime currentTime);
}

public class TimeCalculationService : ITimeCalculationService
{
    private readonly AppDbContext _context;
    
    public TimeCalculationService(AppDbContext context) => _context = context;
    
    public async Task<int> CalculateTimeRemainingAsync(Guid userId, DateOnly date)
    {
        var profile = await _context.TimeProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
        
        if (profile == null) return int.MaxValue;
        
        var dayLimit = GetDailyLimit(profile, date.DayOfWeek);
        if (dayLimit == 0) return int.MaxValue;
        
        var usedToday = await _context.TimeUsage
            .Where(u => u.UserId == userId && u.UsageDate == date)
            .SumAsync(u => u.MinutesUsed);
        
        var adjustments = await _context.TimeAdjustments
            .Where(a => a.UserId == userId && a.AdjustmentDate == date)
            .SumAsync(a => a.MinutesAdjustment);
        
        var dailyRemaining = dayLimit - usedToday + adjustments;
        
        if (profile.WeeklyLimit > 0)
        {
            var weekStart = date.AddDays(-(int)date.DayOfWeek);
            var usedThisWeek = await _context.TimeUsage
                .Where(u => u.UserId == userId && u.UsageDate >= weekStart && u.UsageDate < weekStart.AddDays(7))
                .SumAsync(u => u.MinutesUsed);
            
            var weeklyRemaining = profile.WeeklyLimit - usedThisWeek;
            return Math.Min(dailyRemaining, weeklyRemaining);
        }
        
        return dailyRemaining;
    }
    
    public Task<bool> ShouldEnforceAsync(Guid userId, int timeRemaining) => Task.FromResult(timeRemaining <= 0);
    
    public async Task<bool> IsWithinAllowedHoursAsync(Guid userId, DateTime currentTime)
    {
        var profile = await _context.TimeProfiles
            .Include(p => p.AllowedHours)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
        
        if (profile == null || !profile.AllowedHours.Any())
            return true; // No restrictions = always allowed
        
        var dayOfWeek = (int)currentTime.DayOfWeek;
        var currentTimeOnly = TimeOnly.FromDateTime(currentTime);
        
        var allowed = profile.AllowedHours
            .Where(ah => ah.DayOfWeek == dayOfWeek)
            .Any(ah => currentTimeOnly >= ah.StartTime && currentTimeOnly <= ah.EndTime);
        
        return allowed;
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
