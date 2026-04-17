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
    
    public ClientController(AppDbContext context, ITimeCalculationService timeCalc)
    {
        _context = context;
        _timeCalc = timeCalc;
    }
    
    [HttpPost("register")]
    public async Task<ActionResult<RegisterComputerResponse>> Register(RegisterComputerRequest request)
    {
        var computer = await _context.Computers.FirstOrDefaultAsync(c => c.MachineId == request.MachineId);
        
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
            computer.Hostname = request.Hostname;
            computer.OsInfo = request.OsInfo;
            computer.LastSeenAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        return new RegisterComputerResponse(computer.Id, computer.ApiKey!);
    }
    
    [HttpPost("session/start")]
    public async Task<ActionResult<SessionStartResponse>> StartSession(SessionStartRequest request)
    {
        // Auto-create user if not exists
        await EnsureUserExistsAsync(request.UserId, request.Username);
        
        var session = new Session
        {
            UserId = request.UserId,
            ComputerId = request.ComputerId,
            SessionStart = request.SessionStart
        };
        
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
        
        var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(request.UserId, DateOnly.FromDateTime(request.SessionStart));
        return new SessionStartResponse(session.Id, timeRemaining);
    }
    
    [HttpPost("usage")]
    public async Task<ActionResult<UsageReportResponse>> ReportUsage(UsageReportRequest request)
    {
        // Auto-create user if not exists
        await EnsureUserExistsAsync(request.UserId, request.Username);
        
        var date = DateOnly.FromDateTime(request.Timestamp);
        
        var usage = await _context.TimeUsage
            .FirstOrDefaultAsync(u => u.UserId == request.UserId && u.ComputerId == request.ComputerId && u.UsageDate == date);
        
        if (usage == null)
        {
            usage = new TimeUsage
            {
                UserId = request.UserId,
                ComputerId = request.ComputerId,
                UsageDate = date,
                SessionId = request.SessionId
            };
            _context.TimeUsage.Add(usage);
        }
        
        usage.MinutesUsed += request.MinutesActive;
        usage.LastUpdated = DateTime.UtcNow;
        
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
        
        var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(request.UserId, date);
        var shouldEnforce = await _timeCalc.ShouldEnforceAsync(request.UserId, timeRemaining);
        
        var profile = await _context.TimeProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId && p.IsActive);
        
        return new UsageReportResponse(
            timeRemaining,
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
    
    private async Task EnsureUserExistsAsync(Guid userId, string username)
    {
        var exists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!exists)
        {
            var user = new User
            {
                Id = userId,
                Username = username,
                AccountType = AccountType.Unassigned,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
    }
}
