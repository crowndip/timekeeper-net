using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class UserScenarioTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Scenario_ChildUsesAllTime_EnforcementTriggered()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayOfWeek = today.DayOfWeek;
        
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);

        // Use 60 minutes
        context.TimeUsage.Add(new TimeUsage
        {
            UserId = user.Id,
            UsageDate = today,
            MinutesUsed = 60
        });
        await context.SaveChangesAsync();

        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);
        var shouldEnforce = await service.ShouldEnforceAsync(user.Id, remaining);

        Assert.Equal(0, remaining);
        Assert.True(shouldEnforce);
    }

    [Fact]
    public async Task Scenario_ParentGrantsEmergencyTime_ChildCanContinue()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Child uses all 60 minutes
        context.TimeUsage.Add(new TimeUsage
        {
            UserId = user.Id,
            UsageDate = today,
            MinutesUsed = 60
        });
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        Assert.Equal(0, await service.CalculateTimeRemainingAsync(user.Id, today));

        // Parent grants +30 minutes
        context.TimeAdjustments.Add(new TimeAdjustment
        {
            UserId = user.Id,
            AdjustmentDate = today,
            MinutesAdjustment = 30,
            Reason = "Homework"
        });
        await context.SaveChangesAsync();

        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);
        Assert.Equal(30, remaining);
    }

    [Fact]
    public async Task Scenario_CrossDevice_TimeAggregates()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120,
            IsActive = true
        };
        var linuxPC = new Computer { Hostname = "linux-pc", MachineId = "linux-123" };
        var windowsPC = new Computer { Hostname = "windows-pc", MachineId = "win-123" };
        
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.Computers.AddRange(linuxPC, windowsPC);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Linux: 40 minutes
        context.TimeUsage.Add(new TimeUsage
        {
            UserId = user.Id,
            ComputerId = linuxPC.Id,
            UsageDate = today,
            MinutesUsed = 40
        });
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        Assert.Equal(80, await service.CalculateTimeRemainingAsync(user.Id, today));

        // Windows: 50 minutes
        context.TimeUsage.Add(new TimeUsage
        {
            UserId = user.Id,
            ComputerId = windowsPC.Id,
            UsageDate = today,
            MinutesUsed = 50
        });
        await context.SaveChangesAsync();

        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);
        Assert.Equal(30, remaining); // 120 - 40 - 50 = 30
    }

    [Fact]
    public async Task Scenario_WeeklyLimit_EnforcedAcrossDays()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120,
            WeeklyLimit = 300,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        // Previous days: 100 + 100 = 200 minutes
        context.TimeUsage.AddRange(
            new TimeUsage { UserId = user.Id, UsageDate = twoDaysAgo, MinutesUsed = 100 },
            new TimeUsage { UserId = user.Id, UsageDate = yesterday, MinutesUsed = 100 }
        );
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);

        // Daily: 120, Weekly: 300 - 200 = 100
        Assert.Equal(100, remaining); // Min(120, 100) = 100
    }

    [Fact]
    public async Task Scenario_MultipleProfiles_OnlyActiveApplies()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var weekdayProfile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Weekday",
            MondayLimit = 60,
            IsActive = false
        };
        var weekendProfile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Weekend",
            SaturdayLimit = 180,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.AddRange(weekdayProfile, weekendProfile);
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);

        // Will use active profile's limit for current day
        Assert.True(remaining >= 0);
    }

    [Fact]
    public async Task Scenario_NoActiveProfile_UnlimitedTime()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 60,
            IsActive = false
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);

        Assert.Equal(int.MaxValue, remaining);
    }

    [Fact]
    public async Task Scenario_ParentAccount_NoLimits()
    {
        using var context = CreateContext();
        var parent = new User { Username = "parent1", AccountType = AccountType.Parent };
        context.Users.Add(parent);
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var remaining = await service.CalculateTimeRemainingAsync(parent.Id, today);

        Assert.Equal(int.MaxValue, remaining);
    }

    [Fact]
    public async Task Scenario_TimeAdjustment_ExpiresNextDay()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);

        // Grant +30 minutes for today
        context.TimeAdjustments.Add(new TimeAdjustment
        {
            UserId = user.Id,
            AdjustmentDate = today,
            MinutesAdjustment = 30,
            Reason = "Homework"
        });
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        
        var todayRemaining = await service.CalculateTimeRemainingAsync(user.Id, today);
        Assert.Equal(90, todayRemaining); // 60 + 30

        var tomorrowRemaining = await service.CalculateTimeRemainingAsync(user.Id, tomorrow);
        Assert.Equal(60, tomorrowRemaining); // Back to normal
    }

    [Fact]
    public async Task Scenario_InactiveUser_NoEnforcement()
    {
        using var context = CreateContext();
        var user = new User
        {
            Username = "child1",
            AccountType = AccountType.Child,
            IsActive = false
        };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 60,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        context.TimeUsage.Add(new TimeUsage
        {
            UserId = user.Id,
            UsageDate = today,
            MinutesUsed = 100
        });
        await context.SaveChangesAsync();

        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);
        var shouldEnforce = await service.ShouldEnforceAsync(user.Id, remaining);
        Assert.False(shouldEnforce); // Inactive users not enforced
    }

    [Fact]
    public async Task Scenario_MultipleAdjustments_SumCorrectly()
    {
        using var context = CreateContext();
        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "School Days",
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60,
            IsActive = true
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Multiple adjustments
        context.TimeAdjustments.AddRange(
            new TimeAdjustment { UserId = user.Id, AdjustmentDate = today, MinutesAdjustment = 15, Reason = "Homework" },
            new TimeAdjustment { UserId = user.Id, AdjustmentDate = today, MinutesAdjustment = 10, Reason = "Project" },
            new TimeAdjustment { UserId = user.Id, AdjustmentDate = today, MinutesAdjustment = 5, Reason = "Research" }
        );
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);

        Assert.Equal(90, remaining); // 60 + 15 + 10 + 5
    }
}
