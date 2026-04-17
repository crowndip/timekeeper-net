using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class TimeCalculationServiceTests
{
    private AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CalculateTimeRemaining_NoProfile_ReturnsUnlimited()
    {
        using var context = CreateInMemoryContext();
        var service = new TimeCalculationService(context);
        var userId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await service.CalculateTimeRemainingAsync(userId, date);

        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public async Task CalculateTimeRemaining_WithUsage_SubtractsUsedTime()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var computerId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "testuser", AccountType = AccountType.Child };
        var computer = new Computer { Id = computerId, Hostname = "test", MachineId = "test123" };
        var profile = new TimeProfile
        {
            UserId = userId,
            MondayLimit = 120,
            TuesdayLimit = 120,
            WednesdayLimit = 120,
            ThursdayLimit = 120,
            FridayLimit = 120,
            SaturdayLimit = 120,
            SundayLimit = 120,
            IsActive = true
        };
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var usage = new TimeUsage
        {
            UserId = userId,
            ComputerId = computerId,
            UsageDate = date,
            MinutesUsed = 60
        };

        context.Users.Add(user);
        context.Computers.Add(computer);
        context.TimeProfiles.Add(profile);
        context.TimeUsage.Add(usage);
        await context.SaveChangesAsync();

        var service = new TimeCalculationService(context);
        var result = await service.CalculateTimeRemainingAsync(userId, date);

        Assert.Equal(60, result);
    }

    [Fact]
    public async Task ShouldEnforce_WhenTimeRemaining_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var service = new TimeCalculationService(context);
        var userId = Guid.NewGuid();

        var result = await service.ShouldEnforceAsync(userId, 30);

        Assert.False(result);
    }

    [Fact]
    public async Task ShouldEnforce_WhenNoTimeRemaining_ReturnsTrue()
    {
        using var context = CreateInMemoryContext();
        var service = new TimeCalculationService(context);
        var userId = Guid.NewGuid();

        var result = await service.ShouldEnforceAsync(userId, 0);

        Assert.True(result);
    }
}
