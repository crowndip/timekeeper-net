using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.Shared.DTOs;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;

namespace ParentalControl.WebService.Controllers;

[ApiController]
[Route("api/client")]
public class ClientController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITimeCalculationService _timeCalc;
    private readonly IUserResolutionService _userResolution;
    
    public ClientController(AppDbContext context, ITimeCalculationService timeCalc, IUserResolutionService userResolution)
    {
        _context = context;
        _timeCalc = timeCalc;
        _userResolution = userResolution;
    }
    
    [HttpPost("register")]
    public async Task<ActionResult<RegisterComputerResponse>> Register(RegisterComputerRequest request)
    {
        // Check for existing computer by MachineId OR Hostname (both are unique)
        var computer = await _context.Computers
            .FirstOrDefaultAsync(c => c.MachineId == request.MachineId || c.Hostname == request.Hostname);
        
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
        }
        else
        {
            // Update existing computer
            computer.Hostname = request.Hostname;
            computer.MachineId = request.MachineId;
            computer.OsInfo = request.OsInfo;
            computer.LastSeenAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
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
        var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(primaryUser.Id, DateOnly.FromDateTime(request.SessionStart));
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
        
        // Update computer last seen
        var computer = await _context.Computers.FindAsync(request.ComputerId);
        if (computer != null)
        {
            computer.LastSeenAt = DateTime.UtcNow;
        }
        
        var date = DateOnly.FromDateTime(request.Timestamp);
        var now = DateTime.UtcNow;
        
        // Check for concurrent usage: if another computer reported for this user in the last 60 seconds, don't double-count
        var recentUsage = await _context.TimeUsage
            .Where(u => u.UserId == reportedUserId && u.UsageDate == date && u.ComputerId != request.ComputerId)
            .Where(u => u.LastUpdated >= now.AddSeconds(-60))
            .AnyAsync();
        
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
                SessionId = request.SessionId
            };
            _context.TimeUsage.Add(usage);
        }
        
        // Only add minutes if not concurrent usage
        if (!recentUsage)
        {
            usage.MinutesUsed += request.MinutesActive;
        }
        usage.LastUpdated = now;
        
        if (request.SessionId.HasValue)
        {
            var session = await _context.Sessions.FindAsync(request.SessionId.Value);
            if (session != null)
            {
                session.ActiveMinutes += request.MinutesActive;
                session.IdleMinutes += request.MinutesIdle;
                session.IsActive = request.IsSessionActive;
                session.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync();
        
        // Calculate time using primary user (enforces shared limits)
        var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(primaryUser.Id, date);
        var isWithinAllowedHours = await _timeCalc.IsWithinAllowedHoursAsync(primaryUser.Id, request.Timestamp);
        var minutesUntilAllowedHoursEnd = await _timeCalc.GetMinutesUntilAllowedHoursEndAsync(primaryUser.Id, request.Timestamp);
        
        // Effective time remaining is the minimum of time limit and allowed hours
        var effectiveTimeRemaining = Math.Min(timeRemaining, minutesUntilAllowedHoursEnd);
        
        var shouldEnforce = !isWithinAllowedHours || await _timeCalc.ShouldEnforceAsync(primaryUser.Id, timeRemaining);
        
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
    
    private async Task<Guid> EnsureUserExistsAsync(string username)
    {
        username = username.ToLowerInvariant();
        
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
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
            await _context.SaveChangesAsync();
        }
        return user.Id;
    }
}
