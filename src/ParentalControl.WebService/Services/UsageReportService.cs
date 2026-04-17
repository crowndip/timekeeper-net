using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Services;

public interface IUsageReportService
{
    Task<List<UserUsageReport>> GetUsageReportAsync(DateOnly startDate, DateOnly endDate);
    Task<List<DailyUsageData>> GetDailyUsageAsync(Guid userId, DateOnly startDate, DateOnly endDate);
}

public class UsageReportService : IUsageReportService
{
    private readonly AppDbContext _context;

    public UsageReportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserUsageReport>> GetUsageReportAsync(DateOnly startDate, DateOnly endDate)
    {
        var usage = await _context.TimeUsage
            .Include(u => u.User)
            .Where(u => u.UsageDate >= startDate && u.UsageDate <= endDate)
            .GroupBy(u => new { u.UserId, u.User.Username })
            .Select(g => new UserUsageReport
            {
                UserId = g.Key.UserId,
                Username = g.Key.Username,
                TotalMinutes = g.Sum(u => u.MinutesUsed),
                DailyAverage = g.Average(u => u.MinutesUsed)
            })
            .ToListAsync();

        return usage;
    }

    public async Task<List<DailyUsageData>> GetDailyUsageAsync(Guid userId, DateOnly startDate, DateOnly endDate)
    {
        var usage = await _context.TimeUsage
            .Where(u => u.UserId == userId && u.UsageDate >= startDate && u.UsageDate <= endDate)
            .GroupBy(u => u.UsageDate)
            .Select(g => new DailyUsageData
            {
                Date = g.Key,
                MinutesUsed = g.Sum(u => u.MinutesUsed)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        return usage;
    }
}

public class UserUsageReport
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int TotalMinutes { get; set; }
    public double DailyAverage { get; set; }
}

public class DailyUsageData
{
    public DateOnly Date { get; set; }
    public int MinutesUsed { get; set; }
}
