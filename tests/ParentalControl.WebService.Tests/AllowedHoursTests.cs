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

    [Fact]
    public async Task IsWithinAllowedHours_NoRestrictions_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContext();
        var service = new TimeCalculationService(context);
        var userId = Guid.NewGuid();
        
        // Act
        var result = await service.IsWithinAllowedHoursAsync(userId, DateTime.Now);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsWithinAllowedHours_WithinHours_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = new TimeProfile { UserId = userId, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = new TimeCalculationService(context);
        var testTime = new DateTime(2024, 1, 1, 15, 0, 0); // Monday 3 PM
        
        // Act
        var result = await service.IsWithinAllowedHoursAsync(userId, testTime);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsWithinAllowedHours_OutsideHours_ReturnsFalse()
    {
        // Arrange
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = new TimeProfile { UserId = userId, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = new TimeCalculationService(context);
        var testTime = new DateTime(2024, 1, 1, 23, 0, 0); // Monday 11 PM (outside)
        
        // Act
        var result = await service.IsWithinAllowedHoursAsync(userId, testTime);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_ReturnsCorrectMinutes()
    {
        // Arrange
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = new TimeProfile { UserId = userId, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = new TimeCalculationService(context);
        var testTime = new DateTime(2024, 1, 1, 21, 45, 0); // Monday 9:45 PM
        
        // Act
        var result = await service.GetMinutesUntilAllowedHoursEndAsync(userId, testTime);
        
        // Assert
        Assert.Equal(15, result); // 15 minutes until 10 PM
    }

    [Fact]
    public async Task GetMinutesUntilAllowedHoursEnd_OutsideHours_ReturnsZero()
    {
        // Arrange
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = new TimeProfile { UserId = userId, Name = "Test", IsActive = true };
        context.TimeProfiles.Add(profile);
        
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(22, 0)
        });
        await context.SaveChangesAsync();
        
        var service = new TimeCalculationService(context);
        var testTime = new DateTime(2024, 1, 1, 23, 0, 0); // Monday 11 PM (outside)
        
        // Act
        var result = await service.GetMinutesUntilAllowedHoursEndAsync(userId, testTime);
        
        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task EffectiveTimeRemaining_ShowsMinimum()
    {
        // Arrange
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = new TimeProfile 
        { 
            UserId = userId, 
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
        
        var service = new TimeCalculationService(context);
        var testTime = new DateTime(2024, 1, 1, 21, 45, 0); // Monday 9:45 PM
        
        // Act
        var timeRemaining = await service.CalculateTimeRemainingAsync(userId, DateOnly.FromDateTime(testTime));
        var minutesUntilEnd = await service.GetMinutesUntilAllowedHoursEndAsync(userId, testTime);
        var effectiveTime = Math.Min(timeRemaining, minutesUntilEnd);
        
        // Assert
        Assert.Equal(60, timeRemaining); // Has 60 minutes from daily limit
        Assert.Equal(15, minutesUntilEnd); // But only 15 minutes until allowed hours end
        Assert.Equal(15, effectiveTime); // Should show 15 minutes
    }
}
