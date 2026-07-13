using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParentalControl.WebService.Services;

namespace ParentalControl.WebService.Tests;

// Fixed to UTC so date/time math in tests is deterministic regardless of the host
// machine's local timezone, and so existing tests written against DateTime.UtcNow
// as "today"/"now" keep working unchanged.
public static class TestClock
{
    public static IClockService Utc { get; } = new ClockService(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParentalControl:TimeZoneId"] = "UTC"
            })
            .Build(),
        NullLogger<ClockService>.Instance);
}

// Lets tests control "now" directly, for logic (like concurrent-usage suppression)
// that must key off real wall-clock arrival time rather than a value under test control.
public sealed class MutableTestClock : IClockService
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    public DayOfWeek FirstDayOfWeek => DayOfWeek.Monday;
    public DateTime ToLocal(DateTime utcTime) => DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
    public DateOnly ToLocalDate(DateTime utcTime) => DateOnly.FromDateTime(utcTime);
    public TimeOnly ToLocalTimeOfDay(DateTime utcTime) => TimeOnly.FromDateTime(utcTime);
    public DateOnly LocalToday() => DateOnly.FromDateTime(UtcNow);

    public DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        return date.AddDays(-diff);
    }
}
