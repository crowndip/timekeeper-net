using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class UserTimeStatusTests
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
        var controller = new UsersController(context, userResolution, TestClock.Utc);

        var services = new ServiceCollection();
        services.AddSingleton<ITimeCalculationService>(new TimeCalculationService(context, userResolution, TestClock.Utc));
        var provider = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider }
        };

        return controller;
    }

    [Fact]
    public async Task GetTimeStatus_UserWithAlias_UsedTodayIncludesAliasUsage()
    {
        // Regression test: timeRemaining resolves the whole alias group internally
        // (via CalculateTimeRemainingAsync), but usedToday used to sum only the
        // requested user's own rows -- the two numbers in one response could disagree
        // for anyone with an alias.
        using var context = CreateContext();
        var today = TestClock.Utc.LocalToday();

        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var profile = new TimeProfile
        {
            UserId = primary.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 120, TuesdayLimit = 120, WednesdayLimit = 120, ThursdayLimit = 120,
            FridayLimit = 120, SaturdayLimit = 120, SundayLimit = 120
        };

        context.Users.AddRange(primary, alias);
        context.TimeProfiles.Add(profile);
        context.TimeUsage.Add(new TimeUsage { UserId = primary.Id, UsageDate = today, MinutesUsed = 40 });
        context.TimeUsage.Add(new TimeUsage { UserId = alias.Id, UsageDate = today, MinutesUsed = 25 });
        await context.SaveChangesAsync();

        var controller = CreateController(context);

        // Ask via the primary's id...
        var primaryResult = await controller.GetTimeStatus(primary.Id);
        var (primaryUsedToday, primaryTimeRemaining) = GetUsedTodayAndTimeRemaining(primaryResult);

        // ...and via the alias's id -- both must report the same combined usage.
        var aliasResult = await controller.GetTimeStatus(alias.Id);
        var (aliasUsedToday, aliasTimeRemaining) = GetUsedTodayAndTimeRemaining(aliasResult);

        Assert.Equal(65, primaryUsedToday);
        Assert.Equal(65, aliasUsedToday);
        Assert.Equal(primaryTimeRemaining, aliasTimeRemaining);
    }

    // Avoids `dynamic` here: the controller's response is an anonymous type defined in
    // the WebService assembly, and the dynamic runtime binder enforces accessibility at
    // the call site's assembly, which can throw for anonymous types accessed cross-assembly.
    private static (int UsedToday, int TimeRemaining) GetUsedTodayAndTimeRemaining(IActionResult result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value!;
        var data = value.GetType().GetProperty("data")!.GetValue(value)!;
        var usedToday = (int)data.GetType().GetProperty("usedToday")!.GetValue(data)!;
        var timeRemaining = (int)data.GetType().GetProperty("timeRemaining")!.GetValue(data)!;
        return (usedToday, timeRemaining);
    }
}
