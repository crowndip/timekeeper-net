namespace ParentalControl.Shared.DTOs;

public record RegisterComputerResponse(Guid ComputerId, string ApiKey);

public record UsageReportResponse(
    int TimeRemainingMinutes,
    bool ShouldEnforce,
    string? EnforcementAction,
    int[] WarningMinutes);

public record SessionStartResponse(Guid SessionId, int TimeRemaining);

public record ClientConfigResponse(
    List<UserConfigDto> Users,
    List<TimeProfileDto> Profiles,
    List<AllowedHoursDto> AllowedHours);

public record UserConfigDto(Guid Id, string Username, string AccountType);

public record TimeProfileDto(
    Guid Id,
    Guid UserId,
    string Name,
    int MondayLimit,
    int TuesdayLimit,
    int WednesdayLimit,
    int ThursdayLimit,
    int FridayLimit,
    int SaturdayLimit,
    int SundayLimit,
    int WeeklyLimit,
    string EnforcementAction,
    int[] WarningTimes);

public record AllowedHoursDto(Guid Id, Guid ProfileId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public record DailyUsageResponse(
    Guid UserId,
    string Username,
    DateOnly Date,
    int MinutesUsed,
    int MinutesAdjustment,
    int DailyLimit,
    int TimeRemaining);

public record UsageRecord(
    Guid Id,
    Guid UserId,
    Guid SessionId,
    int MinutesActive,
    int MinutesIdle,
    DateTime Timestamp,
    bool Synced);
