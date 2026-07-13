using Xunit;
using ParentalControl.WebService.Services;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using Microsoft.EntityFrameworkCore;

namespace ParentalControl.WebService.Tests;

public class AllowedHoursTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private TimeCalculationService CreateService(AppDbContext context)
    {
        var userResolution = new UserResolutionService(context);
        return new TimeCalculationService(context, userResolution, TestClock.Utc);
    }

    [Fact]
    public async Task IsWithinAllowedHours_NoRestrictions_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        
        // Act
        var result = await service.IsWithinAllowedHoursAsync(user.Id, DateTime.Now);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsWithinAllowedHours_WithinHours_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 15, 0, 0); // Monday 3 PM
        
        // Act
        var result = await service.IsWithinAllowedHoursAsync(user.Id, testTime);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsWithinAllowedHours_OutsideHours_ReturnsFalse()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 23, 0, 0); // Monday 11 PM (outside)
        
        // Act
        var result = await service.IsWithinAllowedHoursAsync(user.Id, testTime);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_ReturnsCorrectMinutes()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 21, 45, 0); // Monday 9:45 PM
        
        // Act
        var result = await service.GetMinutesUntilAllowedHoursEndAsync(user.Id, testTime);
        
        // Assert
        Assert.Equal(15, result); // 15 minutes until 10 PM
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_AdjacentWindows_MergesAcrossThem()
    {
        // Regression test: with 08:00-12:00 and 12:00-18:00 configured as two separate
        // rows, checking at 11:00 must not report only 60 minutes (until the first
        // window's end) -- allowed time actually continues uninterrupted until 18:00.
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);

        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        });
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(18, 0)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 11, 0, 0); // Monday 11 AM

        var result = await service.GetMinutesUntilAllowedHoursEndAsync(user.Id, testTime);

        // Merged window is 08:00-18:00; 11:00 -> 7 hours = 420 minutes until end.
        Assert.Equal(420, result);
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_OverlappingWindows_MergesAcrossThem()
    {
        // Overlapping (not just touching) windows must also merge correctly.
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);

        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(13, 0)
        });
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(18, 0)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 11, 0, 0); // Monday 11 AM

        var result = await service.GetMinutesUntilAllowedHoursEndAsync(user.Id, testTime);

        // Merged window is 08:00-18:00; 11:00 -> 420 minutes until end.
        Assert.Equal(420, result);
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_NonAdjacentWindows_DoesNotMerge()
    {
        // A genuine gap (e.g. lunch break with no allowed hours) must not be merged away.
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);

        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        });
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(18, 0)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 11, 0, 0); // Monday 11 AM, within first window

        var result = await service.GetMinutesUntilAllowedHoursEndAsync(user.Id, testTime);

        // Still bound by the first window's own end (12:00) -- the gap 12:00-13:00 is real.
        Assert.Equal(60, result);
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_OutsideHours_ReturnsZero()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 23, 0, 0); // Monday 11 PM (outside)
        
        // Act
        var result = await service.GetMinutesUntilAllowedHoursEndAsync(user.Id, testTime);
        
        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task EffectiveTimeRemaining_ShowsMinimum()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        context.Users.Add(user);
        var profile = new TimeProfile 
        { 
            UserId = user.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 60 // 60 minutes daily limit
        };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = CreateService(context);
        var testTime = new DateTime(2024, 1, 1, 21, 45, 0); // Monday 9:45 PM
        
        // Act
        var timeRemaining = await service.CalculateTimeRemainingAsync(user.Id, DateOnly.FromDateTime(testTime));
        var minutesUntilEnd = await service.GetMinutesUntilAllowedHoursEndAsync(user.Id, testTime);
        var effectiveTime = Math.Min(timeRemaining, minutesUntilEnd);
        
        // Assert
        Assert.Equal(60, timeRemaining); // Has 60 minutes from daily limit
        Assert.Equal(15, minutesUntilEnd); // But only 15 minutes until allowed hours end
        Assert.Equal(15, effectiveTime); // Should show 15 minutes
    }
}
