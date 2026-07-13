using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

// Covers the fix for limits being computed against UTC when they are conceptually
// household local time (daily reset time, allowed-hours windows, week boundaries).
public class TimeZoneHandlingTests
{
    private static IClockService CreatePragueClock() => new ClockService(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParentalControl:TimeZoneId"] = "Europe/Prague"
            })
            .Build(),
        NullLogger<ClockService>.Instance);

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AllowedHours_CETWinter_ConvertsUtcToLocalBeforeComparing()
    {
        // Europe/Prague is UTC+1 in winter. An allowed window of 16:00-18:00 local
        // should admit a UTC timestamp of 15:30 (16:30 local) and reject 19:30 UTC.
        using var context = CreateContext();
        var user = new User { Username = "test", AccountType = AccountType.Child };
        var profile = new TimeProfile { UserId = user.Id, Name = "Test", IsActive = true };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.AllowedHours.Add(new AllowedHours
        {
            Profile = profile,
            DayOfWeek = (int)DayOfWeek.Wednesday,
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(18, 0)
        });
        await context.SaveChangesAsync();

        var userResolution = new UserResolutionService(context);
        var service = new TimeCalculationService(context, userResolution, CreatePragueClock());

        // 2026-01-14 is a Wednesday, no DST in January.
        var withinLocalWindow = new DateTime(2026, 1, 14, 15, 30, 0, DateTimeKind.Utc); // 16:30 local
        var outsideLocalWindow = new DateTime(2026, 1, 14, 19, 30, 0, DateTimeKind.Utc); // 20:30 local

        Assert.True(await service.IsWithinAllowedHoursAsync(user.Id, withinLocalWindow));
        Assert.False(await service.IsWithinAllowedHoursAsync(user.Id, outsideLocalWindow));
    }

    [Fact]
    public async Task DailyReset_CET_HappensAtLocalMidnightNotUtcMidnight()
    {
        // A report at 00:30 UTC is 01:30 local (CET, UTC+1) -- still the *same* local
        // day as the evening before, so usage from the evening before should still be
        // grouped into the local-yesterday bucket rather than rolling to a new day.
        var clock = CreatePragueClock();

        var eveningBefore = new DateTime(2026, 1, 14, 22, 0, 0, DateTimeKind.Utc); // 23:00 local, Jan 14
        var justAfterUtcMidnight = new DateTime(2026, 1, 15, 0, 30, 0, DateTimeKind.Utc); // 01:30 local, still Jan 15...

        var localDateEvening = clock.ToLocalDate(eveningBefore);
        var localDateAfterUtcMidnight = clock.ToLocalDate(justAfterUtcMidnight);

        // 22:00 UTC Jan 14 -> 23:00 local Jan 14. 00:30 UTC Jan 15 -> 01:30 local Jan 15.
        // The point under test: local midnight (23:00 UTC Jan 14), not UTC midnight
        // (00:00 UTC Jan 15), is where the day actually rolls over locally.
        Assert.Equal(new DateOnly(2026, 1, 14), localDateEvening);

        var justBeforeLocalMidnight = new DateTime(2026, 1, 14, 22, 45, 0, DateTimeKind.Utc); // 23:45 local
        var justAfterLocalMidnight = new DateTime(2026, 1, 14, 23, 15, 0, DateTimeKind.Utc); // 00:15 local Jan 15

        Assert.Equal(new DateOnly(2026, 1, 14), clock.ToLocalDate(justBeforeLocalMidnight));
        Assert.Equal(new DateOnly(2026, 1, 15), clock.ToLocalDate(justAfterLocalMidnight));
        Assert.Equal(new DateOnly(2026, 1, 15), localDateAfterUtcMidnight);
    }

    [Fact]
    public void FirstDayOfWeek_DefaultsToMonday()
    {
        var clock = new ClockService(new ConfigurationBuilder().Build(), NullLogger<ClockService>.Instance);
        Assert.Equal(DayOfWeek.Monday, clock.FirstDayOfWeek);

        var wednesday = new DateOnly(2026, 4, 15);
        Assert.Equal(new DateOnly(2026, 4, 13), clock.GetWeekStart(wednesday)); // preceding Monday
    }

    [Fact]
    public async Task WeeklyLimit_AdjustmentGrantedEarlierInWeek_AppliesToLaterDays()
    {
        // Regression test for #13: a +60 minute bonus granted on Monday must still count
        // toward the weekly total on Tuesday, not just the day it was granted on.
        using var context = CreateContext();
        var monday = new DateOnly(2026, 4, 13);
        var tuesday = monday.AddDays(1);

        var user = new User { Username = "child1", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test",
            IsActive = true,
            MondayLimit = 120, TuesdayLimit = 120, WednesdayLimit = 120, ThursdayLimit = 120,
            FridayLimit = 120, SaturdayLimit = 120, SundayLimit = 120,
            WeeklyLimit = 200
        };
        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.TimeUsage.Add(new TimeUsage { UserId = user.Id, UsageDate = monday, MinutesUsed = 150 });
        context.TimeAdjustments.Add(new TimeAdjustment
        {
            UserId = user.Id,
            AdjustmentDate = monday,
            MinutesAdjustment = 60,
            CreatedBy = "Parent"
        });
        await context.SaveChangesAsync();

        var userResolution = new UserResolutionService(context);
        var service = new TimeCalculationService(context, userResolution, TestClock.Utc);

        var remaining = await service.CalculateTimeRemainingAsync(user.Id, tuesday);

        // Daily: 120 (no usage yet Tuesday). Weekly: 200 - 150 + 60 = 110. Min(120, 110) = 110.
        // Before the fix, Monday's adjustment was invisible on Tuesday (only *today's*
        // adjustments were summed), giving weekly = 200 - 150 = 50 and a wrong answer of 50.
        Assert.Equal(110, remaining);
    }
}
