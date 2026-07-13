namespace ParentalControl.Shared.DTOs;

// Lets the client-API-key filter (RequireClientApiKeyAttribute) find which computer a
// request is about without reflection, so it can check the caller's key against that
// specific computer's key.
public interface IHasComputerId
{
    Guid ComputerId { get; }
}

public record RegisterComputerRequest(string Hostname, string MachineId, string OsInfo);

public record UsageReportRequest(
    Guid ComputerId,
    Guid UserId,
    string Username,
    Guid? SessionId,
    DateTime Timestamp,
    int MinutesActive,
    int MinutesIdle,
    bool IsSessionActive) : IHasComputerId;

public record CreateUserRequest(string Username, string? FullName, string? Email, string AccountType);

public record UpdateUserRequest(string? FullName, string? Email, string AccountType, bool IsActive);

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

public record SessionStartRequest(Guid ComputerId, Guid UserId, string Username, DateTime SessionStart) : IHasComputerId;

public record SessionEndRequest(Guid SessionId, DateTime SessionEnd, string TerminationReason);
