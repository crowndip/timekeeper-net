namespace ParentalControl.WebService.Services;

// Limits (daily reset, allowed hours, weekly windows) are conceptually household
// local time even though clients report UTC timestamps. This is the single place
// that converts between the two, so daily/weekly boundaries land at local midnight
// instead of UTC midnight.
public interface IClockService
{
    DayOfWeek FirstDayOfWeek { get; }
    // Real wall-clock instant, routed through here (instead of DateTime.UtcNow at call
    // sites) so time-sensitive server logic (e.g. concurrent-usage suppression, which
    // must key off when a report actually arrived, not the timestamp the client claims)
    // can be exercised deterministically in tests.
    DateTime UtcNow { get; }
    DateTime ToLocal(DateTime utcTime);
    DateOnly ToLocalDate(DateTime utcTime);
    TimeOnly ToLocalTimeOfDay(DateTime utcTime);
    DateOnly LocalToday();
    DateOnly GetWeekStart(DateOnly date);
}

public class ClockService : IClockService
{
    private readonly TimeZoneInfo _timeZone;

    public DayOfWeek FirstDayOfWeek { get; }

    public DateTime UtcNow => DateTime.UtcNow;

    public ClockService(IConfiguration configuration, ILogger<ClockService> logger)
    {
        var tzId = configuration["ParentalControl:TimeZoneId"];
        if (string.IsNullOrWhiteSpace(tzId))
        {
            _timeZone = TimeZoneInfo.Local;
        }
        else
        {
            try
            {
                _timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                logger.LogWarning(ex, "Configured ParentalControl:TimeZoneId '{TimeZoneId}' is invalid, falling back to server local time", tzId);
                _timeZone = TimeZoneInfo.Local;
            }
        }

        var firstDaySetting = configuration["ParentalControl:FirstDayOfWeek"];
        FirstDayOfWeek = Enum.TryParse<DayOfWeek>(firstDaySetting, ignoreCase: true, out var parsed)
            ? parsed
            : DayOfWeek.Monday;
    }

    public DateTime ToLocal(DateTime utcTime)
    {
        var utc = utcTime.Kind switch
        {
            DateTimeKind.Local => utcTime.ToUniversalTime(),
            DateTimeKind.Utc => utcTime,
            _ => DateTime.SpecifyKind(utcTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    public DateOnly ToLocalDate(DateTime utcTime) => DateOnly.FromDateTime(ToLocal(utcTime));

    public TimeOnly ToLocalTimeOfDay(DateTime utcTime) => TimeOnly.FromDateTime(ToLocal(utcTime));

    public DateOnly LocalToday() => ToLocalDate(UtcNow);

    public DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        return date.AddDays(-diff);
    }
}
