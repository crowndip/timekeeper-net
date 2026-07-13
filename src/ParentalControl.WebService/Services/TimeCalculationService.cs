using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Services;

public interface ITimeCalculationService
{
    Task<int> CalculateTimeRemainingAsync(Guid userId, DateOnly date);
    bool ShouldEnforce(int timeRemaining);
    Task<bool> IsWithinAllowedHoursAsync(Guid userId, DateTime currentTime);
    Task<int> GetMinutesUntilAllowedHoursEndAsync(Guid userId, DateTime currentTime);
}

public class TimeCalculationService : ITimeCalculationService
{
    private readonly AppDbContext _context;
    private readonly IUserResolutionService _userResolution;
    private readonly IClockService _clock;

    public TimeCalculationService(AppDbContext context, IUserResolutionService userResolution, IClockService clock)
    {
        _context = context;
        _userResolution = userResolution;
        _clock = clock;
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
            var weekStart = _clock.GetWeekStart(date);
            var weekEnd = weekStart.AddDays(7);
            var usedThisWeek = await _context.TimeUsage
                .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate >= weekStart && u.UsageDate < weekEnd)
                .SumAsync(u => u.MinutesUsed);

            var weeklyAdjustments = await _context.TimeAdjustments
                .Where(a => allUserIds.Contains(a.UserId) && a.AdjustmentDate >= weekStart && a.AdjustmentDate < weekEnd)
                .SumAsync(a => a.MinutesAdjustment);

            var weeklyRemaining = profile.WeeklyLimit - usedThisWeek + weeklyAdjustments;
            return Math.Min(dailyRemaining, weeklyRemaining);
        }

        return dailyRemaining;
    }
    
    public bool ShouldEnforce(int timeRemaining) => timeRemaining < 0;
    
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

        var localTime = _clock.ToLocal(currentTime);
        var dayOfWeek = (int)localTime.DayOfWeek;
        var currentTimeOnly = TimeOnly.FromDateTime(localTime);

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

        var localTime = _clock.ToLocal(currentTime);
        var dayOfWeek = (int)localTime.DayOfWeek;
        var currentTimeOnly = TimeOnly.FromDateTime(localTime);

        // Merge adjacent/overlapping windows first: with 08:00-12:00 and 12:00-18:00
        // configured as two separate rows, a naive "find the window containing now" at
        // 11:00 would report only 60 minutes left (until the first window's end) even
        // though allowed time actually continues uninterrupted until 18:00.
        var todayWindows = profile.AllowedHours.Where(ah => ah.DayOfWeek == dayOfWeek);
        var merged = MergeWindows(todayWindows);

        var containingIndex = merged.FindIndex(w => currentTimeOnly >= w.Start && currentTimeOnly <= w.End);
        if (containingIndex < 0)
            return 0; // Outside allowed hours

        var minutesUntilEnd = (int)(merged[containingIndex].End.ToTimeSpan() - currentTimeOnly.ToTimeSpan()).TotalMinutes;
        return minutesUntilEnd;
    }

    private static List<(TimeOnly Start, TimeOnly End)> MergeWindows(IEnumerable<AllowedHours> windows)
    {
        var sorted = windows.OrderBy(w => w.StartTime).ToList();
        var merged = new List<(TimeOnly Start, TimeOnly End)>();

        foreach (var window in sorted)
        {
            if (merged.Count > 0 && window.StartTime <= merged[^1].End)
            {
                // Overlaps or touches the previous window -- extend it rather than
                // treating this as a separate window with its own earlier "end".
                if (window.EndTime > merged[^1].End)
                    merged[^1] = (merged[^1].Start, window.EndTime);
            }
            else
            {
                merged.Add((window.StartTime, window.EndTime));
            }
        }

        return merged;
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
