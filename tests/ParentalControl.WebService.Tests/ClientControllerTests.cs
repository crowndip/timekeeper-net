using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ParentalControl.Shared.DTOs;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class ClientControllerTests
{
    private AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Register_NewComputer_ReturnsComputerId()
    {
        using var context = CreateInMemoryContext();
        var timeCalc = new TimeCalculationService(context);
        var controller = new ClientController(context, timeCalc);
        var request = new RegisterComputerRequest("testhost", "machine123", "Linux");

        var result = await controller.Register(request);

        var okResult = Assert.IsType<ActionResult<RegisterComputerResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.NotEqual(Guid.Empty, okResult.Value.ComputerId);
    }

    [Fact]
    public async Task GetConfig_ReturnsOnlyChildAccounts()
    {
        using var context = CreateInMemoryContext();
        var child = new User { Username = "child", AccountType = AccountType.Child, IsActive = true };
        var parent = new User { Username = "parent", AccountType = AccountType.Parent, IsActive = true };
        
        context.Users.AddRange(child, parent);
        await context.SaveChangesAsync();

        var timeCalc = new TimeCalculationService(context);
        var controller = new ClientController(context, timeCalc);

        var result = await controller.GetConfig(Guid.NewGuid());

        var okResult = Assert.IsType<ActionResult<ClientConfigResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Single(okResult.Value.Users);
        Assert.Equal("child", okResult.Value.Users[0].Username);
    }

    [Fact]
    public async Task ReportUsage_ValidRequest_ReturnsTimeRemaining()
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

        context.Users.Add(user);
        context.Computers.Add(computer);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var timeCalc = new TimeCalculationService(context);
        var controller = new ClientController(context, timeCalc);
        var request = new UsageReportRequest(computerId, userId, null, DateTime.UtcNow, 5, 0, true);

        var result = await controller.ReportUsage(request);

        var okResult = Assert.IsType<ActionResult<UsageReportResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.True(okResult.Value.TimeRemainingMinutes > 0);
    }
}
