using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ParentalControl.Shared.DTOs;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Filters;
using ParentalControl.WebService.Models;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class RequireClientApiKeyAttributeTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ActionExecutingContext CreateActionContext(
        AppDbContext db,
        bool requireKey,
        string? apiKeyHeader,
        string actionName,
        IDictionary<string, object?> actionArguments,
        object? routeComputerId = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ParentalControl:RequireClientApiKey"] = requireKey.ToString() })
            .Build());
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        if (apiKeyHeader != null)
            httpContext.Request.Headers["X-Api-Key"] = apiKeyHeader;

        var routeData = new RouteData();
        if (routeComputerId != null)
            routeData.Values["computerId"] = routeComputerId;

        var actionDescriptor = new ActionDescriptor
        {
            RouteValues = new Dictionary<string, string?> { ["action"] = actionName }
        };

        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), actionArguments, controller: new object());
    }

    [Fact]
    public async Task Register_IsAlwaysExempt_RegardlessOfKeyOrMode()
    {
        using var db = CreateContext();
        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: true, apiKeyHeader: null, actionName: "Register", new Dictionary<string, object?>());

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task GraceMode_MissingKey_IsAllowed()
    {
        using var db = CreateContext();
        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: false, apiKeyHeader: null, actionName: "ReportUsage",
            new Dictionary<string, object?> { ["request"] = new UsageReportRequest(Guid.NewGuid(), Guid.NewGuid(), "kid", null, DateTime.UtcNow, 1, 0, true) });

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task EnforcedMode_MissingKey_IsRejected()
    {
        using var db = CreateContext();
        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: true, apiKeyHeader: null, actionName: "ReportUsage",
            new Dictionary<string, object?> { ["request"] = new UsageReportRequest(Guid.NewGuid(), Guid.NewGuid(), "kid", null, DateTime.UtcNow, 1, 0, true) });

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.False(nextCalled);
        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task WrongKey_IsAlwaysRejected_EvenInGraceMode()
    {
        using var db = CreateContext();
        var computer = new Computer { Hostname = "pc1", MachineId = "m1", ApiKey = "correct-key" };
        db.Computers.Add(computer);
        await db.SaveChangesAsync();

        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: false, apiKeyHeader: "wrong-key", actionName: "ReportUsage",
            new Dictionary<string, object?> { ["request"] = new UsageReportRequest(computer.Id, Guid.NewGuid(), "kid", null, DateTime.UtcNow, 1, 0, true) });

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.False(nextCalled);
        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task CorrectKey_MatchingComputerId_IsAllowed()
    {
        using var db = CreateContext();
        var computer = new Computer { Hostname = "pc1", MachineId = "m1", ApiKey = "correct-key" };
        db.Computers.Add(computer);
        await db.SaveChangesAsync();

        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: true, apiKeyHeader: "correct-key", actionName: "ReportUsage",
            new Dictionary<string, object?> { ["request"] = new UsageReportRequest(computer.Id, Guid.NewGuid(), "kid", null, DateTime.UtcNow, 1, 0, true) });

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task CorrectKey_ForADifferentComputer_IsRejected()
    {
        // A valid key for computer A must not authorize a request claiming to be computer B.
        using var db = CreateContext();
        var computerA = new Computer { Hostname = "pc-a", MachineId = "m-a", ApiKey = "key-a" };
        var computerB = new Computer { Hostname = "pc-b", MachineId = "m-b", ApiKey = "key-b" };
        db.Computers.AddRange(computerA, computerB);
        await db.SaveChangesAsync();

        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: true, apiKeyHeader: "key-a", actionName: "ReportUsage",
            new Dictionary<string, object?> { ["request"] = new UsageReportRequest(computerB.Id, Guid.NewGuid(), "kid", null, DateTime.UtcNow, 1, 0, true) });

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.False(nextCalled);
        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task RouteComputerId_IsUsed_ForConfigEndpoint()
    {
        using var db = CreateContext();
        var computer = new Computer { Hostname = "pc1", MachineId = "m1", ApiKey = "correct-key" };
        db.Computers.Add(computer);
        await db.SaveChangesAsync();

        var filter = new RequireClientApiKeyAttribute();
        var nextCalled = false;

        var context = CreateActionContext(db, requireKey: true, apiKeyHeader: "correct-key", actionName: "GetConfig",
            new Dictionary<string, object?>(), routeComputerId: computer.Id.ToString());

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.True(nextCalled);
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext context) =>
        new(context, new List<IFilterMetadata>(), context.Controller);
}
