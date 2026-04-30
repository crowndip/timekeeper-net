using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.Shared.DTOs;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class ClientAliasIntegrationTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private ClientController CreateController(AppDbContext context)
    {
        var userResolution = new UserResolutionService(context);
        var timeCalculation = new TimeCalculationService(context, userResolution);
        return new ClientController(context, timeCalculation, userResolution);
    }

    [Fact]
    public async Task Register_Computer_Success()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        
        var request = new RegisterComputerRequest("test-pc", "machine-123", "Linux");
        var result = await controller.Register(request);

        var response = Assert.IsType<RegisterComputerResponse>(result.Value);
        Assert.NotEqual(Guid.Empty, response.ComputerId);
        Assert.NotNull(response.ApiKey);
    }

    [Fact]
    public async Task StartSession_AliasUsername_TracksReportedUser()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
        context.Users.AddRange(primary, alias);
        context.Computers.Add(computer);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var request = new SessionStartRequest(computer.Id, alias.Id, "joe", DateTime.UtcNow);

        var result = await controller.StartSession(request);

        var response = Assert.IsType<SessionStartResponse>(result.Value);
        var session = await context.Sessions.FirstOrDefaultAsync(s => s.ComputerId == computer.Id);
        
        Assert.NotNull(session);
        Assert.Equal(alias.Id, session.UserId);
    }

    [Fact]
    public async Task ReportUsage_AliasUsername_AggregatesWithPrimary()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
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
        context.Computers.Add(computer);
        context.TimeProfiles.Add(profile);
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, ComputerId = computer.Id, UsageDate = today, MinutesUsed = 50 });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var request = new UsageReportRequest(
            computer.Id,
            alias.Id,
            "joe",
            null,
            DateTime.UtcNow,
            30,
            0,
            true
        );

        var result = await controller.ReportUsage(request);

        var response = Assert.IsType<UsageReportResponse>(result.Value);
        
        // 120 - (50 + 30) = 40
        Assert.Equal(40, response.TimeRemainingMinutes);
        
        // Verify usage recorded under alias
        var aliasUsage = await context.TimeUsage
            .FirstOrDefaultAsync(u => u.UserId == alias.Id && u.UsageDate == today);
        Assert.NotNull(aliasUsage);
        Assert.Equal(30, aliasUsage.MinutesUsed);
    }

    [Fact]
    public async Task FullFlow_CreateAlias_UseOnBothAccounts_Unlink()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        // Setup
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
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
        context.Computers.Add(computer);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        // Step 1: Create alias
        var usersController = new UsersController(context, new UserResolutionService(context));
        var addResult = await usersController.AddAlias(primary.Id, alias.Id);
        Assert.IsType<OkObjectResult>(addResult);

        // Step 2: Use as primary
        var clientController = CreateController(context);
        var primaryUsage = new UsageReportRequest(
            computer.Id,
            primary.Id,
            "john",
            null,
            DateTime.UtcNow,
            50,
            0,
            true
        );
        await clientController.ReportUsage(primaryUsage);

        // Step 3: Use as alias
        var aliasUsage = new UsageReportRequest(
            computer.Id,
            alias.Id,
            "joe",
            null,
            DateTime.UtcNow,
            30,
            0,
            true
        );
        var aliasResult = await clientController.ReportUsage(aliasUsage);
        var aliasResponse = Assert.IsType<UsageReportResponse>(aliasResult.Value);
        
        // Should see aggregated usage: 120 - 80 = 40
        Assert.Equal(40, aliasResponse.TimeRemainingMinutes);

        // Step 4: Unlink alias
        var unlinkResult = await usersController.RemoveAlias(alias.Id);
        Assert.IsType<OkObjectResult>(unlinkResult);

        // Step 5: Use as independent user (no profile = unlimited)
        var independentUsage = new UsageReportRequest(
            computer.Id,
            alias.Id,
            "joe",
            null,
            DateTime.UtcNow,
            10,
            0,
            true
        );
        var independentResult = await clientController.ReportUsage(independentUsage);
        var independentResponse = Assert.IsType<UsageReportResponse>(independentResult.Value);
        
        Assert.Equal(int.MaxValue, independentResponse.TimeRemainingMinutes);
    }

    [Fact]
    public async Task ConcurrentUsage_MultipleAliases_AllEnforced()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias1 = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var alias2 = new User { Username = "johnny", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 100,
            TuesdayLimit = 100,
            WednesdayLimit = 100,
            ThursdayLimit = 100,
            FridayLimit = 100,
            SaturdayLimit = 100,
            SundayLimit = 100
        };
        
        context.Users.AddRange(primary, alias1, alias2);
        context.TimeProfiles.Add(profile);
        context.Computers.Add(new Computer { Hostname = "pc1", MachineId = "m1" });
        context.Computers.Add(new Computer { Hostname = "pc2", MachineId = "m2" });
        context.Computers.Add(new Computer { Hostname = "pc3", MachineId = "m3" });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var pc1 = await context.Computers.FirstAsync(c => c.Hostname == "pc1");
        var pc2 = await context.Computers.FirstAsync(c => c.Hostname == "pc2");
        var pc3 = await context.Computers.FirstAsync(c => c.Hostname == "pc3");

        // Use 40 minutes on primary
        await controller.ReportUsage(new UsageReportRequest(
            pc1.Id, primary.Id, "john", null, DateTime.UtcNow, 40, 0, true));

        // Use 35 minutes on alias1
        await controller.ReportUsage(new UsageReportRequest(
            pc2.Id, alias1.Id, "joe", null, DateTime.UtcNow, 35, 0, true));

        // Use 25 minutes on alias2
        var result = await controller.ReportUsage(new UsageReportRequest(
            pc3.Id, alias2.Id, "johnny", null, DateTime.UtcNow, 25, 0, true));

        var response = Assert.IsType<UsageReportResponse>(result.Value);
        
        // 100 - (40 + 35 + 25) = 0
        Assert.Equal(0, response.TimeRemainingMinutes);
        Assert.False(response.ShouldEnforce); // Should NOT enforce at exactly 0
    }
}
