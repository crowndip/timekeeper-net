using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        var timeCalculation = new TimeCalculationService(context, userResolution, TestClock.Utc);
        return new ClientController(context, timeCalculation, userResolution, TestClock.Utc, NullLogger<ClientController>.Instance);
    }

    [Fact]
    public async Task ReportUsage_MixedCaseUsername_ResolvesToSameNormalizedUser()
    {
        // Regression test for #8: a parent creating "Alice" in the UI and a client
        // reporting "alice" must resolve to the same user, not create a split identity.
        using var context = CreateContext();
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
        context.Computers.Add(computer);
        await context.SaveChangesAsync();

        var usersController = new UsersController(context, new UserResolutionService(context), TestClock.Utc);
        var createResult = await usersController.CreateUser(new CreateUserRequest("Alice", null, null, "Child"));
        Assert.IsType<OkObjectResult>(createResult);

        var storedUser = await context.Users.SingleAsync();
        Assert.Equal("alice", storedUser.Username); // normalized on create

        var controller = CreateController(context);
        var request = new UsageReportRequest(computer.Id, Guid.Empty, "alice", null, DateTime.UtcNow, 5, 0, true);
        await controller.ReportUsage(request);

        // Still exactly one user -- the client's lowercase report matched the normalized row.
        Assert.Equal(1, await context.Users.CountAsync());
        var usage = await context.TimeUsage.SingleAsync();
        Assert.Equal(storedUser.Id, usage.UserId);
    }

    [Fact]
    public async Task ReportUsage_ConcurrentFirstContactForNewUsername_DoesNotFail()
    {
        // Regression test for #16: two computers reporting a brand-new username at the
        // same instant must not surface a 500 to either client -- registration/auto-user-
        // creation must never be refused.
        var dbName = Guid.NewGuid().ToString();
        AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

        using var setupContext = NewContext();
        var pc1 = new Computer { Hostname = "pc1", MachineId = "m1" };
        var pc2 = new Computer { Hostname = "pc2", MachineId = "m2" };
        setupContext.Computers.AddRange(pc1, pc2);
        await setupContext.SaveChangesAsync();

        // Two separate DbContext instances (as two separate concurrent requests would have),
        // both unaware of each other, both about to insert the same brand-new username.
        using var contextA = NewContext();
        using var contextB = NewContext();

        var controllerA = new ClientController(
            contextA, new TimeCalculationService(contextA, new UserResolutionService(contextA), TestClock.Utc),
            new UserResolutionService(contextA), TestClock.Utc, NullLogger<ClientController>.Instance);
        var controllerB = new ClientController(
            contextB, new TimeCalculationService(contextB, new UserResolutionService(contextB), TestClock.Utc),
            new UserResolutionService(contextB), TestClock.Utc, NullLogger<ClientController>.Instance);

        var requestA = new UsageReportRequest(pc1.Id, Guid.Empty, "newkid", null, DateTime.UtcNow, 1, 0, true);
        var requestB = new UsageReportRequest(pc2.Id, Guid.Empty, "newkid", null, DateTime.UtcNow, 1, 0, true);

        var resultA = await controllerA.ReportUsage(requestA);
        var resultB = await controllerB.ReportUsage(requestB);

        Assert.IsType<UsageReportResponse>(resultA.Value);
        Assert.IsType<UsageReportResponse>(resultB.Value);

        using var verifyContext = NewContext();
        Assert.Equal(1, await verifyContext.Users.CountAsync(u => u.Username == "newkid"));
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
    public async Task Register_SameMachineId_IsIdempotent_ReturnsSameApiKey()
    {
        // Regression test for #18/goal 2: registration must never be refused, and calling
        // it again on every client startup (the actual usage pattern) must return the
        // same computer record and key, not create a duplicate or a new key.
        using var context = CreateContext();
        var controller = CreateController(context);

        var first = await controller.Register(new RegisterComputerRequest("test-pc", "machine-123", "Linux"));
        var firstResponse = Assert.IsType<RegisterComputerResponse>(first.Value);

        var second = await controller.Register(new RegisterComputerRequest("test-pc", "machine-123", "Linux"));
        var secondResponse = Assert.IsType<RegisterComputerResponse>(second.Value);

        Assert.Equal(firstResponse.ComputerId, secondResponse.ComputerId);
        Assert.Equal(firstResponse.ApiKey, secondResponse.ApiKey);
        Assert.Equal(1, await context.Computers.CountAsync());
    }

    [Fact]
    public async Task Register_HostnameCollision_DoesNotHijackOtherComputersRecord()
    {
        // Regression test for #18: matching on Hostname (in addition to MachineId) let a
        // hostname collision (e.g. two machines named "family-pc" after a reinstall)
        // silently take over another machine's record and ApiKey. Must match on
        // MachineId only.
        using var context = CreateContext();
        var controller = CreateController(context);

        var original = await controller.Register(new RegisterComputerRequest("family-pc", "machine-original", "Linux"));
        var originalResponse = Assert.IsType<RegisterComputerResponse>(original.Value);

        var collision = await controller.Register(new RegisterComputerRequest("family-pc", "machine-new", "Linux"));
        var collisionResponse = Assert.IsType<RegisterComputerResponse>(collision.Value);

        Assert.NotEqual(originalResponse.ComputerId, collisionResponse.ComputerId);
        Assert.NotEqual(originalResponse.ApiKey, collisionResponse.ApiKey);
        Assert.Equal(2, await context.Computers.CountAsync());

        // The original computer's record (and its ApiKey) must be untouched.
        var originalComputer = await context.Computers.FindAsync(originalResponse.ComputerId);
        Assert.NotNull(originalComputer);
        Assert.Equal("machine-original", originalComputer.MachineId);
        Assert.Equal(originalResponse.ApiKey, originalComputer.ApiKey);
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
        var usersController = new UsersController(context, new UserResolutionService(context), TestClock.Utc);
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
    public async Task ConcurrentUsage_MultipleAliases_SameInstant_OnlyFirstCounted()
    {
        // A child using three different alias logins across three different computers at the
        // same moment must still only burn one wall-clock minute of the shared budget, not three
        // (see ClientController.ReportUsage: recentUsage is checked across the whole alias group).
        using var context = CreateContext();

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

        // All three reports land within the same instant (well inside the concurrency window).
        await controller.ReportUsage(new UsageReportRequest(
            pc1.Id, primary.Id, "john", null, DateTime.UtcNow, 40, 0, true));

        await controller.ReportUsage(new UsageReportRequest(
            pc2.Id, alias1.Id, "joe", null, DateTime.UtcNow, 35, 0, true));

        var result = await controller.ReportUsage(new UsageReportRequest(
            pc3.Id, alias2.Id, "johnny", null, DateTime.UtcNow, 25, 0, true));

        var response = Assert.IsType<UsageReportResponse>(result.Value);

        // Only the first (winning) report's 40 minutes should count: 100 - 40 = 60.
        Assert.Equal(60, response.TimeRemainingMinutes);
    }

    [Fact]
    public async Task ConcurrentUsage_TwoComputers_AlternatingReports_CountsWallClockTimeOnce()
    {
        // Two computers reporting on offset 30s schedules for 10 simulated minutes must count
        // exactly 10 minutes total -- not 20 (double-counted) and not near-zero (the v1.67.0
        // mutual-suppression deadlock, where LastUpdated was refreshed even when suppressed).
        using var context = CreateContext();

        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 1440, TuesdayLimit = 1440, WednesdayLimit = 1440, ThursdayLimit = 1440,
            FridayLimit = 1440, SaturdayLimit = 1440, SundayLimit = 1440
        };
        var pc1 = new Computer { Hostname = "pc1", MachineId = "m1" };
        var pc2 = new Computer { Hostname = "pc2", MachineId = "m2" };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.Computers.AddRange(pc1, pc2);
        await context.SaveChangesAsync();

        var userResolution = new UserResolutionService(context);
        var fakeClock = new MutableTestClock { UtcNow = DateTime.UtcNow };
        var timeCalculation = new TimeCalculationService(context, userResolution, fakeClock);
        var controller = new ClientController(context, timeCalculation, userResolution, fakeClock, NullLogger<ClientController>.Instance);

        // Reports fire every 60s per computer, offset by 30s from each other -- so combined,
        // one report arrives every 30 simulated real seconds, alternating pc1/pc2, each
        // covering 1 "minute" of activity since the previous report from that computer.
        for (var i = 0; i < 20; i++)
        {
            var computer = i % 2 == 0 ? pc1 : pc2;
            fakeClock.UtcNow = fakeClock.UtcNow.AddSeconds(i == 0 ? 0 : 30);
            await controller.ReportUsage(new UsageReportRequest(
                computer.Id, user.Id, "child1", null, fakeClock.UtcNow, 1, 0, true));
        }

        var totalUsed = await context.TimeUsage
            .Where(u => u.UserId == user.Id)
            .SumAsync(u => u.MinutesUsed);

        Assert.Equal(10, totalUsed);
    }

    [Fact]
    public async Task ConcurrentUsage_SingleComputer_EveryReportCounted()
    {
        using var context = CreateContext();

        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 1440, TuesdayLimit = 1440, WednesdayLimit = 1440, ThursdayLimit = 1440,
            FridayLimit = 1440, SaturdayLimit = 1440, SundayLimit = 1440
        };
        var pc1 = new Computer { Hostname = "pc1", MachineId = "m1" };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.Computers.Add(pc1);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var start = DateTime.UtcNow;

        for (var i = 0; i < 10; i++)
        {
            var timestamp = start.AddMinutes(i);
            await controller.ReportUsage(new UsageReportRequest(
                pc1.Id, user.Id, "child1", null, timestamp, 1, 0, true));
        }

        var totalUsed = await context.TimeUsage
            .Where(u => u.UserId == user.Id)
            .SumAsync(u => u.MinutesUsed);

        Assert.Equal(10, totalUsed);
    }

    [Fact]
    public async Task ConcurrentUsage_LockedMachineZeroReports_DoesNotStarveActiveMachine()
    {
        // Regression test for review finding #2: a zero-minute report (e.g. a locked
        // session's idle tick) must never claim the concurrency window. Before the fix,
        // machine B (locked, reporting 0 active minutes every tick) would win the window
        // every time simply by reporting first, permanently suppressing machine A's real
        // minutes -- i.e. unlimited time as long as a second machine sits locked and logged in.
        using var context = CreateContext();

        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 1440, TuesdayLimit = 1440, WednesdayLimit = 1440, ThursdayLimit = 1440,
            FridayLimit = 1440, SaturdayLimit = 1440, SundayLimit = 1440
        };
        var pcA = new Computer { Hostname = "pc-active", MachineId = "m-active" };
        var pcB = new Computer { Hostname = "pc-locked", MachineId = "m-locked" };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.Computers.AddRange(pcA, pcB);
        await context.SaveChangesAsync();

        var userResolution = new UserResolutionService(context);
        var fakeClock = new MutableTestClock { UtcNow = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc) };
        var timeCalculation = new TimeCalculationService(context, userResolution, fakeClock);
        var controller = new ClientController(context, timeCalculation, userResolution, fakeClock, NullLogger<ClientController>.Instance);

        // Each simulated minute: locked machine B reports first (0 active, 1 idle), then 30s
        // later active machine A reports its 1 real minute.
        for (var i = 0; i < 10; i++)
        {
            if (i > 0) fakeClock.UtcNow = fakeClock.UtcNow.AddSeconds(30);
            await controller.ReportUsage(new UsageReportRequest(
                pcB.Id, user.Id, "child1", null, fakeClock.UtcNow, 0, 1, true));

            fakeClock.UtcNow = fakeClock.UtcNow.AddSeconds(30);
            await controller.ReportUsage(new UsageReportRequest(
                pcA.Id, user.Id, "child1", null, fakeClock.UtcNow, 1, 0, true));
        }

        var totalUsed = await context.TimeUsage
            .Where(u => u.UserId == user.Id)
            .SumAsync(u => u.MinutesUsed);

        // Every one of A's 10 real minutes must count; B's zero reports never claimed
        // the window, so they never suppressed A.
        Assert.Equal(10, totalUsed);
    }

    [Fact]
    public async Task ConcurrentUsage_SingleZeroProbe_DoesNotSuppressFollowingActiveReport()
    {
        using var context = CreateContext();

        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 1440, TuesdayLimit = 1440, WednesdayLimit = 1440, ThursdayLimit = 1440,
            FridayLimit = 1440, SaturdayLimit = 1440, SundayLimit = 1440
        };
        var pcA = new Computer { Hostname = "pc-active", MachineId = "m-active" };
        var pcB = new Computer { Hostname = "pc-probe", MachineId = "m-probe" };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.Computers.AddRange(pcA, pcB);
        await context.SaveChangesAsync();

        var userResolution = new UserResolutionService(context);
        var fakeClock = new MutableTestClock { UtcNow = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc) };
        var timeCalculation = new TimeCalculationService(context, userResolution, fakeClock);
        var controller = new ClientController(context, timeCalculation, userResolution, fakeClock, NullLogger<ClientController>.Instance);

        // A single zero-active probe (e.g. CheckTimeRemainingAsync on a new session)...
        await controller.ReportUsage(new UsageReportRequest(
            pcB.Id, user.Id, "child1", null, fakeClock.UtcNow, 0, 0, true));

        // ...must not suppress a real report from another computer 30s later.
        fakeClock.UtcNow = fakeClock.UtcNow.AddSeconds(30);
        await controller.ReportUsage(new UsageReportRequest(
            pcA.Id, user.Id, "child1", null, fakeClock.UtcNow, 1, 0, true));

        var totalUsed = await context.TimeUsage
            .Where(u => u.UserId == user.Id)
            .SumAsync(u => u.MinutesUsed);

        Assert.Equal(1, totalUsed);
    }

    [Fact]
    public async Task ConcurrentUsage_OldBacklogFlush_CountsInFullAndDoesNotSuppressLiveActivity()
    {
        // Regression test for review finding #3: a backlog batch flushed after the server
        // (or that machine) was offline carries an old Timestamp. It must count in full
        // (the minutes really happened) but must NOT claim the current concurrency window --
        // otherwise it would suppress another machine's live activity for minutes that
        // already happened elsewhere, permanently losing them (the client marks a 200
        // response as synced regardless of server-side suppression).
        using var context = CreateContext();

        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 1440, TuesdayLimit = 1440, WednesdayLimit = 1440, ThursdayLimit = 1440,
            FridayLimit = 1440, SaturdayLimit = 1440, SundayLimit = 1440
        };
        var pcA = new Computer { Hostname = "pc-active", MachineId = "m-active" };
        var pcB = new Computer { Hostname = "pc-backlog", MachineId = "m-backlog" };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.Computers.AddRange(pcA, pcB);
        await context.SaveChangesAsync();

        var userResolution = new UserResolutionService(context);
        var fakeClock = new MutableTestClock { UtcNow = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc) };
        var timeCalculation = new TimeCalculationService(context, userResolution, fakeClock);
        var controller = new ClientController(context, timeCalculation, userResolution, fakeClock, NullLogger<ClientController>.Instance);

        // A reports 1 live minute right now.
        await controller.ReportUsage(new UsageReportRequest(
            pcA.Id, user.Id, "child1", null, fakeClock.UtcNow, 1, 0, true));

        // 30s later, B flushes a 30-minute backlog batch timestamped 6 hours ago (offline
        // stretch). It must count in full despite A's very recent live report.
        fakeClock.UtcNow = fakeClock.UtcNow.AddSeconds(30);
        var backlogTimestamp = fakeClock.UtcNow.AddHours(-6);
        await controller.ReportUsage(new UsageReportRequest(
            pcB.Id, user.Id, "child1", null, backlogTimestamp, 30, 0, true));

        var afterBacklog = await context.TimeUsage
            .Where(u => u.UserId == user.Id)
            .SumAsync(u => u.MinutesUsed);
        Assert.Equal(31, afterBacklog);

        // 30s after that, A's next live minute must still count -- the backlog flush must
        // not have claimed the current window on B's behalf.
        fakeClock.UtcNow = fakeClock.UtcNow.AddSeconds(30);
        await controller.ReportUsage(new UsageReportRequest(
            pcA.Id, user.Id, "child1", null, fakeClock.UtcNow, 1, 0, true));

        var afterSecondLiveReport = await context.TimeUsage
            .Where(u => u.UserId == user.Id)
            .SumAsync(u => u.MinutesUsed);
        Assert.Equal(32, afterSecondLiveReport);
    }
}
