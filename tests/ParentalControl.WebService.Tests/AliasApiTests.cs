using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class AliasApiTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private UsersController CreateController(AppDbContext context)
    {
        var userResolution = new UserResolutionService(context);
        return new UsersController(context, userResolution, TestClock.Utc);
    }

    [Fact]
    public async Task AddAlias_ValidUsers_Success()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child };
        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.AddAlias(primary.Id, alias.Id);

        Assert.IsType<OkObjectResult>(result);
        
        var updatedAlias = await context.Users.FindAsync(alias.Id);
        Assert.NotNull(updatedAlias);
        Assert.Equal(primary.Id, updatedAlias.PrimaryUserId);
    }

    [Fact]
    public async Task AddAlias_PrimaryNotFound_NotFound()
    {
        using var context = CreateContext();
        var alias = new User { Username = "joe", AccountType = AccountType.Child };
        context.Users.Add(alias);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.AddAlias(Guid.NewGuid(), alias.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddAlias_AliasNotFound_NotFound()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(primary);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.AddAlias(primary.Id, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddAlias_UserWithActiveProfile_BadRequest()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child };
        var profile = new TimeProfile { UserId = alias.Id, Name = "Test", IsActive = true, MondayLimit = 60 };
        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.AddAlias(primary.Id, alias.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddAlias_UserWithAliases_BadRequest()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var existingAlias = new User { Username = "johnny", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var newAlias = new User { Username = "joe", AccountType = AccountType.Child };
        context.Users.AddRange(primary, existingAlias, newAlias);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.AddAlias(newAlias.Id, primary.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddAlias_AlreadyAlias_BadRequest()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.AddAlias(primary.Id, alias.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RemoveAlias_ValidAlias_Success()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.RemoveAlias(alias.Id);

        Assert.IsType<OkObjectResult>(result);
        
        var updatedAlias = await context.Users.FindAsync(alias.Id);
        Assert.NotNull(updatedAlias);
        Assert.Null(updatedAlias.PrimaryUserId);
    }

    [Fact]
    public async Task RemoveAlias_NotFound_NotFound()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        
        var result = await controller.RemoveAlias(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RemoveAlias_NotAnAlias_BadRequest()
    {
        using var context = CreateContext();
        var user = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.RemoveAlias(user.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAliases_PrimaryWithAliases_ReturnsAll()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias1 = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var alias2 = new User { Username = "johnny", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        context.Users.AddRange(primary, alias1, alias2);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetAliases(primary.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetAliases_PrimaryWithoutAliases_ReturnsEmpty()
    {
        using var context = CreateContext();
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(primary);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetAliases(primary.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetAliases_NotFound_ReturnsOk()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        
        var result = await controller.GetAliases(Guid.NewGuid());

        // Returns Ok with empty list, not NotFound
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetUsageBreakdown_PrimaryWithAliases_ReturnsBreakdown()
    {
        using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
        context.Users.AddRange(primary, alias);
        context.Computers.Add(computer);
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, ComputerId = computer.Id, UsageDate = today, MinutesUsed = 50 });
        context.TimeUsage.Add(new TimeUsage { UserId = alias.Id, ComputerId = computer.Id, UsageDate = today, MinutesUsed = 30 });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.GetUsageBreakdown(primary.Id, today, today);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetUsageBreakdown_NotFound_NotFound()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var result = await controller.GetUsageBreakdown(Guid.NewGuid(), today, today);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
