namespace ParentalControl.Shared.DTOs;

public record RegisterComputerRequest(string Hostname, string MachineId, string OsInfo);

public record UsageReportRequest(
    Guid ComputerId,
    Guid UserId,
    Guid? SessionId,
    DateTime Timestamp,
    int MinutesActive,
    int MinutesIdle,
    bool IsSessionActive);

public record CreateUserRequest(string Username, string FullName, string? Email, string AccountType);

public record CreateTimeProfileRequest(
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

public record TimeAdjustmentRequest(Guid UserId, DateOnly Date, int MinutesAdjustment, string Reason);

public record SessionStartRequest(Guid ComputerId, Guid UserId, DateTime SessionStart);

public record SessionEndRequest(Guid SessionId, DateTime SessionEnd, string TerminationReason);
