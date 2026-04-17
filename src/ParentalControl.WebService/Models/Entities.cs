using System.ComponentModel.DataAnnotations;

namespace ParentalControl.WebService.Models;

public enum AccountType
{
    Unassigned = -1, // Auto-created, needs parent configuration
    Child = 0,       // Supervised with limits
    Parent = 1,      // No limits, can be admin
    Technical = 2    // System/service accounts, no limits
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(64)]
    public string Username { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? FullName { get; set; }
    
    [MaxLength(255)]
    public string? Email { get; set; }
    
    public AccountType AccountType { get; set; } = AccountType.Child;
    
    [Obsolete("Use AccountType instead")]
    public bool IsSupervised
    {
        get => AccountType == AccountType.Child;
        set => AccountType = value ? AccountType.Child : AccountType.Parent;
    }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<TimeProfile> TimeProfiles { get; set; } = new List<TimeProfile>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

public class Computer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(255)]
    public string Hostname { get; set; } = string.Empty;
    
    [Required, MaxLength(64)]
    public string MachineId { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? OsInfo { get; set; }
    
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(128)]
    public string? ApiKey { get; set; }
    
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

public class TimeProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public int MondayLimit { get; set; }
    public int TuesdayLimit { get; set; }
    public int WednesdayLimit { get; set; }
    public int ThursdayLimit { get; set; }
    public int FridayLimit { get; set; }
    public int SaturdayLimit { get; set; }
    public int SundayLimit { get; set; }
    public int WeeklyLimit { get; set; }
    
    [MaxLength(20)]
    public string EnforcementAction { get; set; } = "logout";
    
    public int[] WarningTimes { get; set; } = [15, 10, 5, 1];
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User User { get; set; } = null!;
    public ICollection<AllowedHours> AllowedHours { get; set; } = new List<AllowedHours>();
}

public class AllowedHours
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    
    public TimeProfile Profile { get; set; } = null!;
}

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ComputerId { get; set; }
    
    public DateTime SessionStart { get; set; }
    public DateTime? SessionEnd { get; set; }
    
    public int ActiveMinutes { get; set; }
    public int IdleMinutes { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(50)]
    public string? TerminationReason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User User { get; set; } = null!;
    public Computer Computer { get; set; } = null!;
}

public class TimeUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ComputerId { get; set; }
    public Guid? SessionId { get; set; }
    
    public DateOnly UsageDate { get; set; }
    public int MinutesUsed { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    public User User { get; set; } = null!;
    public Computer Computer { get; set; } = null!;
}

public class TimeAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly AdjustmentDate { get; set; }
    public int MinutesAdjustment { get; set; }
    
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public User User { get; set; } = null!;
}
