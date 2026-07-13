using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParentalControl.Shared.DTOs;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Filters;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/client")]
[RequireClientApiKey]
public class ClientController : ControllerBase
{
    // Concurrent-usage suppression window: must be larger than the client's reporting
    // interval (60s) so a single computer's on-time report is never mistaken for a
    // second computer, but small enough that switching computers doesn't lose a tick.
    private static readonly TimeSpan ConcurrentUsageWindow = TimeSpan.FromSeconds(90);

    private readonly AppDbContext _context;
    private readonly ITimeCalculationService _timeCalc;
    private readonly IUserResolutionService _userResolution;
    private readonly IClockService _clock;
    private readonly ILogger<ClientController> _logger;

    public ClientController(
        AppDbContext context,
        ITimeCalculationService timeCalc,
        IUserResolutionService userResolution,
        IClockService clock,
        ILogger<ClientController> logger)
    {
        _context = context;
        _timeCalc = timeCalc;
        _userResolution = userResolution;
        _clock = clock;
        _logger = logger;
    }
    
    [HttpPost("register")]
    public async Task<ActionResult<RegisterComputerResponse>> Register(RegisterComputerRequest request)
    {
        // Match on MachineId only. Hostname is user-controlled and not a safe identity
        // key: two machines named "family-pc" after a reinstall is a realistic scenario,
        // and matching on it let a hostname collision silently hijack another machine's
        // record (and its ApiKey). Hostname is stored as display metadata only.
        //
        // Registration must never be refused (a curious child on the LAN discovering the
        // ApiKey isn't the threat model here -- the protected network + nginx reverse
        // proxy is): this always creates-or-returns a working computer record.
        var computer = await _context.Computers
            .FirstOrDefaultAsync(c => c.MachineId == request.MachineId);

        if (computer == null)
        {
            computer = new Computer
            {
                Hostname = request.Hostname,
                MachineId = request.MachineId,
                OsInfo = request.OsInfo,
                ApiKey = Guid.NewGuid().ToString("N")
            };
            _context.Computers.Add(computer);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Two computers (or two startups of the same client) racing to register the
                // same brand-new MachineId at once: re-query for the row the other request
                // just created instead of surfacing a 500.
                _logger.LogInformation(ex, "Concurrent registration race for MachineId '{MachineId}', re-querying", request.MachineId);
                _context.Entry(computer).State = EntityState.Detached;

                computer = await _context.Computers.FirstOrDefaultAsync(c => c.MachineId == request.MachineId);
                if (computer == null) throw;
            }
        }
        else
        {
            // Update display metadata on the existing computer; identity (MachineId) never changes.
            computer.Hostname = request.Hostname;
            computer.OsInfo = request.OsInfo;
            computer.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return new RegisterComputerResponse(computer.Id, computer.ApiKey!);
    }
    
    [HttpPost("session/start")]
    public async Task<ActionResult<SessionStartResponse>> StartSession(SessionStartRequest request)
    {
        // Get or create user by username (reported user)
        var reportedUserId = await EnsureUserExistsAsync(request.Username);
        
        // Resolve to primary user for time calculations
        var primaryUser = await _userResolution.ResolveToPrimaryAsync(reportedUserId);
        if (primaryUser == null) return NotFound("User not found");
        
        // Update last seen on reported user (shows which login was used)
        var reportedUser = await _context.Users.FindAsync(reportedUserId);
        if (reportedUser != null)
        {
            reportedUser.UpdatedAt = DateTime.UtcNow;
        }
        
        var session = new Session
        {
            UserId = reportedUserId, // Keep original user for audit trail
            ComputerId = request.ComputerId,
            SessionStart = request.SessionStart
        };
        
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // Calculate time using primary user (enforces shared limits)
        var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(primaryUser.Id, _clock.ToLocalDate(request.SessionStart));
        return new SessionStartResponse(session.Id, timeRemaining);
    }
    
    [HttpPost("usage")]
    public async Task<ActionResult<UsageReportResponse>> ReportUsage(UsageReportRequest request)
    {
        if (request.MinutesActive < 0 || request.MinutesActive > 1440)
            return BadRequest("Invalid MinutesActive value");
        
        if (request.MinutesIdle < 0 || request.MinutesIdle > 1440)
            return BadRequest("Invalid MinutesIdle value");
        
        // Get or create user by username (reported user)
        var reportedUserId = await EnsureUserExistsAsync(request.Username);

        // Resolve to primary user for time calculations
        var primaryUser = await _userResolution.ResolveToPrimaryAsync(reportedUserId);
        if (primaryUser == null) return NotFound("User not found");

        // Update computer last seen -- outside the concurrency-control transaction below,
        // since it's not part of the read-check-write race that transaction protects.
        var computer = await _context.Computers.FindAsync(request.ComputerId);
        if (computer != null)
        {
            computer.LastSeenAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        var date = _clock.ToLocalDate(request.Timestamp);
        var now = _clock.UtcNow;

        // A child's time budget is shared wall-clock time across every computer they use
        // concurrently, under the alias group (the same person may report under multiple
        // usernames). Only one computer may "win" and count a given window; the others are
        // suppressed for that window only, so a full minute is never counted twice and never
        // counted zero times.
        var allUserIds = await _userResolution.GetAllUserIdsInGroupAsync(primaryUser.Id);

        await RecordUsageWithConcurrencyControlAsync(request, reportedUserId, allUserIds, date, now);

        // Calculate time using primary user (enforces shared limits)
        var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(primaryUser.Id, date);
        var isWithinAllowedHours = await _timeCalc.IsWithinAllowedHoursAsync(primaryUser.Id, request.Timestamp);
        var minutesUntilAllowedHoursEnd = await _timeCalc.GetMinutesUntilAllowedHoursEndAsync(primaryUser.Id, request.Timestamp);
        
        // Effective time remaining is the minimum of time limit and allowed hours
        var effectiveTimeRemaining = Math.Min(timeRemaining, minutesUntilAllowedHoursEnd);
        
        var shouldEnforce = !isWithinAllowedHours || _timeCalc.ShouldEnforce(timeRemaining);
        
        var profile = await _context.TimeProfiles.FirstOrDefaultAsync(p => p.UserId == primaryUser.Id && p.IsActive);
        
        return new UsageReportResponse(
            effectiveTimeRemaining,
            shouldEnforce,
            shouldEnforce ? profile?.EnforcementAction : null,
            profile?.WarningTimes ?? []
        );
    }
    
    [HttpPost("session/end")]
    public async Task<IActionResult> EndSession(SessionEndRequest request)
    {
        var session = await _context.Sessions.FindAsync(request.SessionId);
        if (session == null) return NotFound();
        
        session.SessionEnd = request.SessionEnd;
        session.IsActive = false;
        session.TerminationReason = request.TerminationReason;
        session.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return Ok();
    }
    
    [HttpGet("config/{computerId}")]
    public async Task<ActionResult<ClientConfigResponse>> GetConfig(Guid computerId)
    {
        var users = await _context.Users
            .Where(u => u.IsActive && u.AccountType == Models.AccountType.Child)
            .Select(u => new UserConfigDto(u.Id, u.Username, u.AccountType.ToString()))
            .ToListAsync();
        
        var profiles = await _context.TimeProfiles
            .Where(p => p.IsActive)
            .Select(p => new TimeProfileDto(
                p.Id, p.UserId, p.Name,
                p.MondayLimit, p.TuesdayLimit, p.WednesdayLimit, p.ThursdayLimit,
                p.FridayLimit, p.SaturdayLimit, p.SundayLimit, p.WeeklyLimit,
                p.EnforcementAction, p.WarningTimes))
            .ToListAsync();
        
        var allowedHours = await _context.AllowedHours
            .Select(a => new AllowedHoursDto(a.Id, a.ProfileId, a.DayOfWeek, a.StartTime, a.EndTime))
            .ToListAsync();
        
        return new ClientConfigResponse(users, profiles, allowedHours);
    }
    
    // Wraps the read-check-write race (recentUsage check -> count -> save) in Serializable
    // isolation on PostgreSQL, so two reports for the same alias group landing within the
    // same few milliseconds can't both read "no recent usage" and both count -- one of
    // them fails with a serialization error and is retried once.
    //
    // The InMemory provider (used by the test suite) supports neither transactions nor
    // isolation levels, so it runs the same counting logic (RecordUsageCoreAsync) directly
    // with no transaction wrapper -- this means the test suite cannot catch a regression
    // in the transaction/retry plumbing itself, only in the shared counting logic.
    private async Task RecordUsageWithConcurrencyControlAsync(
        UsageReportRequest request, Guid reportedUserId, List<Guid> allUserIds, DateOnly date, DateTime now)
    {
        if (!_context.Database.IsRelational())
        {
            await RecordUsageCoreAsync(request, reportedUserId, allUserIds, date, now);
            await _context.SaveChangesAsync();
            return;
        }

        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await RecordUsageCoreAsync(request, reportedUserId, allUserIds, date, now);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsSerializationFailure(ex))
            {
                await transaction.RollbackAsync();

                // Stale tracked entities are the classic bug on a retry: detach everything
                // so RecordUsageCoreAsync re-queries fresh state in the next attempt.
                foreach (var entry in _context.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;

                _logger.LogInformation(ex, "Serialization failure recording usage for computer {ComputerId}, retrying", request.ComputerId);
            }
            // If the retry also fails, the exception is not caught here and propagates --
            // the client already treats a 500 as "offline this tick" and retries next tick,
            // which is itself a correct outcome for a race this rare.
        }
    }

    private static bool IsSerializationFailure(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is PostgresException pg && pg.SqlState == "40001")
                return true;
        }
        return false;
    }

    // The actual concurrent-usage counting logic (see the class-level comment on
    // ConcurrentUsageWindow). Shared unchanged between the relational (transactional) and
    // InMemory (non-transactional) paths in RecordUsageWithConcurrencyControlAsync -- only
    // the transaction wrapper differs, not the business logic.
    private async Task RecordUsageCoreAsync(
        UsageReportRequest request, Guid reportedUserId, List<Guid> allUserIds, DateOnly date, DateTime now)
    {
        // "Live" = this report describes activity happening right now (a normal 60s tick).
        // A report whose timestamp is older than the concurrency window is a backlog flush
        // (client was offline / server was down) describing PAST windows -- it must neither
        // be suppressed by, nor claim, the CURRENT window, or a machine catching up loses
        // its minutes permanently (the client marks a 200 response as synced regardless).
        // request.Timestamp is client-controlled, but it's our own client and a skewed clock
        // fails no worse than before this fix.
        var isLive = now - request.Timestamp <= ConcurrentUsageWindow;

        var recentUsage = await _context.TimeUsage
            .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate == date && u.ComputerId != request.ComputerId)
            .Where(u => u.LastUpdated >= now.Subtract(ConcurrentUsageWindow))
            .AnyAsync();

        var suppressed = isLive && recentUsage;

        // Record usage with reported user ID (audit trail)
        var usage = await _context.TimeUsage
            .FirstOrDefaultAsync(u => u.UserId == reportedUserId && u.ComputerId == request.ComputerId && u.UsageDate == date);

        if (usage == null)
        {
            usage = new TimeUsage
            {
                UserId = reportedUserId, // Keep original user for audit trail
                ComputerId = request.ComputerId,
                UsageDate = date,
                SessionId = request.SessionId,
                // Not DateTime.MinValue: Kind=Unspecified throws in Npgsql when written to a
                // timestamptz column (the InMemory test provider does NOT catch this -- it
                // does not enforce DateTime.Kind at all). UnixEpoch is Kind=Utc and reads as
                // "never claimed" just as clearly.
                LastUpdated = DateTime.UnixEpoch
            };
            _context.TimeUsage.Add(usage);
        }

        // Count and claim only when real minutes were reported and this report wasn't
        // suppressed as concurrent. A zero-active report (locked-session tick, new-session
        // probe, startup user sync) must NEVER claim the window -- otherwise a second
        // machine left logged-in-but-locked claims every window with 0-minute reports and
        // the actively-used machine's real minutes are suppressed forever, i.e. unlimited
        // time. And a backlog flush counts its (past) minutes but must not claim the
        // CURRENT window, or it would suppress another machine's live activity for minutes
        // that already happened somewhere else.
        if (!suppressed && request.MinutesActive > 0)
        {
            usage.MinutesUsed += request.MinutesActive;
            if (isLive)
                usage.LastUpdated = now;
        }

        if (request.SessionId.HasValue)
        {
            var session = await _context.Sessions.FindAsync(request.SessionId.Value);
            if (session != null)
            {
                // Keep session totals consistent with TimeUsage: only accrue when this
                // report's minutes were actually counted toward the child's budget.
                if (!suppressed && request.MinutesActive > 0)
                {
                    session.ActiveMinutes += request.MinutesActive;
                }
                session.IdleMinutes += request.MinutesIdle;
                session.IsActive = request.IsSessionActive;
                session.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task<Guid> EnsureUserExistsAsync(string username)
    {
        username = ValidationHelper.NormalizeUsername(username);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user != null) return user.Id;

        // Registration/user-creation must never be refused: two computers can report the
        // same brand-new username at the same instant. Create optimistically and, if the
        // unique-index insert loses the race, fall back to re-reading the row the other
        // request just created instead of surfacing a 500 to the client.
        user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            AccountType = AccountType.Unassigned,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogInformation(ex, "Concurrent auto-creation race for username '{Username}', re-querying", username);
            _context.Entry(user).State = EntityState.Detached;

            user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) throw;
        }

        return user.Id;
    }
}
