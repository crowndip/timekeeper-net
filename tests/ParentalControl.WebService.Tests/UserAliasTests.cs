using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class UserAliasTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private UserResolutionService CreateResolutionService(AppDbContext context)
    {
        return new UserResolutionService(context);
    }

    private TimeCalculationService CreateTimeService(AppDbContext context)
    {
        var userResolution = new UserResolutionService(context);
        return new TimeCalculationService(context, userResolution);
    }

    [Fact]
    public async Task ResolveToPrimary_PrimaryUser_ReturnsSelf()
    {
        using var context = CreateContext();
        var user = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.ResolveToPrimaryAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("john", result.Username);
    }

    [Fact]
    public async Task ResolveToPrimary_AliasUser_ReturnsPrimary()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.ResolveToPrimaryAsync(alias.Id);

        Assert.NotNull(result);
        Assert.Equal(primary.Id, result.Id);
        Assert.Equal("john", result.Username);
    }

    [Fact]
    public async Task GetAllUserIds_PrimaryWithAliases_ReturnsAll()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias1 = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var alias2 = new User { Username = "john@email.com", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        context.Users.AddRange(primary, alias1, alias2);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.GetAllUserIdsInGroupAsync(primary.Id);

        Assert.Equal(3, result.Count);
        Assert.Contains(primary.Id, result);
        Assert.Contains(alias1.Id, result);
        Assert.Contains(alias2.Id, result);
    }

    [Fact]
    public async Task GetAllUserIds_PrimaryWithoutAliases_ReturnsSelf()
    {
        using var context = CreateContext();
        var user = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.GetAllUserIdsInGroupAsync(user.Id);

        Assert.Single(result);
        Assert.Contains(user.Id, result);
    }

    [Fact]
    public async Task CanBecomeAlias_UserWithAliases_ReturnsFalse()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.CanBecomeAliasAsync(primary.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task CanBecomeAlias_UserWithActiveProfile_ReturnsFalse()
    {
        using var context = CreateContext();
        var user = new User { Username = "john", AccountType = AccountType.Child };
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true, MondayLimit = 60 };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.CanBecomeAliasAsync(user.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task CanBecomeAlias_IndependentUser_ReturnsTrue()
    {
        using var context = CreateContext();
        var user = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateResolutionService(context);
        var result = await service.CanBecomeAliasAsync(user.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task TimeCalculation_AggregatesUsageAcrossAliases()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120
        };
        
        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profile);
        
        // Primary user used 50 minutes
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, UsageDate = today, MinutesUsed = 50 });
        // Alias used 30 minutes
        context.TimeUsage.Add(new TimeUsage { UserId = alias.Id, UsageDate = today, MinutesUsed = 30 });
        
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        var remaining = await service.CalculateTimeRemainingAsync(primary.Id, today);

        // 120 - (50 + 30) = 40
        Assert.Equal(40, remaining);
    }

    [Fact]
    public async Task TimeCalculation_AliasUser_UsesSharedLimit()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120
        };
        
        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profile);
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, UsageDate = today, MinutesUsed = 80 });
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        // Calculate for alias user
        var remaining = await service.CalculateTimeRemainingAsync(alias.Id, today);

        // Should get same result as primary: 120 - 80 = 40
        Assert.Equal(40, remaining);
    }

    [Fact]
    public async Task TimeCalculation_WeeklyLimit_AggregatesAliases()
    {
        using var context = CreateContext();
        var monday = new DateOnly(2026, 4, 14); // Monday
        var tuesday = monday.AddDays(1);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120,
            WeeklyLimit = 300
        };
        
        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profile);
        
        // Monday: primary 100, alias 50 = 150 total
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, UsageDate = monday, MinutesUsed = 100 });
        context.TimeUsage.Add(new TimeUsage { UserId = alias.Id, UsageDate = monday, MinutesUsed = 50 });
        
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        var remaining = await service.CalculateTimeRemainingAsync(primary.Id, tuesday);

        // Daily: 120, Weekly: 300 - 150 = 150
        // Min(120, 150) = 120
        Assert.Equal(120, remaining);
    }

    [Fact]
    public async Task TimeCalculation_InactiveUser_UnlimitedTime()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var user = new User { Username = "john", AccountType = AccountType.Child, IsActive = false };
        var profile = new TimeProfile 
        { 
            UserId = user.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60
        };
        
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.TimeUsage.Add(new TimeUsage { UserId = user.Id, UsageDate = today, MinutesUsed = 100 });
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        var remaining = await service.CalculateTimeRemainingAsync(user.Id, today);

        Assert.Equal(int.MaxValue, remaining);
    }

    [Fact]
    public async Task AllowedHours_AliasUser_UsesPrimarySchedule()
    {
        using var context = CreateContext();
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile { UserId = primary.Id, Name = "Test", IsActive = true, MondayLimit = 120 };
        var allowedHours = new AllowedHours
        {
            ProfileId = profile.Id,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };
        
        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profile);
        context.AllowedHours.Add(allowedHours);
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        var testTime = new DateTime(2024, 1, 1, 10, 0, 0); // Monday 10 AM
        
        // Check for alias user
        var result = await service.IsWithinAllowedHoursAsync(alias.Id, testTime);

        Assert.True(result);
    }

    [Fact]
    public async Task MultipleAliases_AllShareSameLimit()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias1 = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var alias2 = new User { Username = "john@email.com", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120
        };
        
        context.Users.AddRange(primary, alias1, alias2);
        context.TimeProfiles.Add(profile);
        
        // Each user uses some time
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, UsageDate = today, MinutesUsed = 30 });
        context.TimeUsage.Add(new TimeUsage { UserId = alias1.Id, UsageDate = today, MinutesUsed = 40 });
        context.TimeUsage.Add(new TimeUsage { UserId = alias2.Id, UsageDate = today, MinutesUsed = 20 });
        
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        
        // All should return same remaining time
        var remainingPrimary = await service.CalculateTimeRemainingAsync(primary.Id, today);
        var remainingAlias1 = await service.CalculateTimeRemainingAsync(alias1.Id, today);
        var remainingAlias2 = await service.CalculateTimeRemainingAsync(alias2.Id, today);

        // 120 - (30 + 40 + 20) = 30
        Assert.Equal(30, remainingPrimary);
        Assert.Equal(30, remainingAlias1);
        Assert.Equal(30, remainingAlias2);
    }

    [Fact]
    public async Task UnlinkAlias_BecomesIndependent()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profilePrimary = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Primary Profile", 
            IsActive = true,
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120
        };
        
        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profilePrimary);
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, UsageDate = today, MinutesUsed = 50 });
        context.TimeUsage.Add(new TimeUsage { UserId = alias.Id, UsageDate = today, MinutesUsed = 30 });
        await context.SaveChangesAsync();

        var service = CreateTimeService(context);
        
        // Before unlink: shared limit
        var remainingBefore = await service.CalculateTimeRemainingAsync(alias.Id, today);
        Assert.Equal(40, remainingBefore); // 120 - 80 = 40

        // Unlink alias
        alias.PrimaryUserId = null;
        await context.SaveChangesAsync();

        // After unlink: no profile = unlimited
        var remainingAfter = await service.CalculateTimeRemainingAsync(alias.Id, today);
        Assert.Equal(int.MaxValue, remainingAfter);
    }
}
