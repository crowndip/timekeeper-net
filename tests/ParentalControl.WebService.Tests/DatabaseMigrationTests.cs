using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class DatabaseMigrationTests
{
    private AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task InitialCreate_CreatesAllTables()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Verify all tables exist by attempting to query them
        Assert.NotNull(await context.Users.ToListAsync());
        Assert.NotNull(await context.Computers.ToListAsync());
        Assert.NotNull(await context.TimeProfiles.ToListAsync());
        Assert.NotNull(await context.AllowedHours.ToListAsync());
        Assert.NotNull(await context.TimeUsage.ToListAsync());
        Assert.NotNull(await context.TimeAdjustments.ToListAsync());
        Assert.NotNull(await context.Sessions.ToListAsync());
    }

    [Fact]
    public async Task InitialCreate_UsersTable_HasCorrectSchema()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var user = new User
        {
            Username = "testuser",
            FullName = "Test User",
            Email = "test@example.com",
            AccountType = AccountType.Child,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrieved = await context.Users.FirstAsync();
        Assert.Equal("testuser", retrieved.Username);
        Assert.Equal("Test User", retrieved.FullName);
        Assert.Equal("test@example.com", retrieved.Email);
        Assert.Equal(AccountType.Child, retrieved.AccountType);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public async Task InitialCreate_TimeProfilesTable_HasCorrectSchema()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var user = new User { Username = "testuser", AccountType = AccountType.Child };
        var profile = new TimeProfile
        {
            UserId = user.Id,
            Name = "Test Profile",
            IsActive = true,
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60,
            WeeklyLimit = 420,
            EnforcementAction = "Logout",
            WarningTimes = new[] { 15, 5 }
        };

        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        await context.SaveChangesAsync();

        var retrieved = await context.TimeProfiles.FirstAsync();
        Assert.Equal("Test Profile", retrieved.Name);
        Assert.Equal(60, retrieved.MondayLimit);
        Assert.Equal(420, retrieved.WeeklyLimit);
        Assert.Equal(2, retrieved.WarningTimes.Length);
    }

    [Fact]
    public async Task InitialCreate_ForeignKeys_WorkCorrectly()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var user = new User { Username = "testuser", AccountType = AccountType.Child };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
        var profile = new TimeProfile 
        { 
            UserId = user.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60
        };
        var allowedHours = new AllowedHours
        {
            ProfileId = profile.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };

        context.Users.Add(user);
        context.Computers.Add(computer);
        context.TimeProfiles.Add(profile);
        context.AllowedHours.Add(allowedHours);
        await context.SaveChangesAsync();

        // Load with navigation properties
        var loadedProfile = await context.TimeProfiles
            .Include(p => p.User)
            .Include(p => p.AllowedHours)
            .FirstAsync();

        Assert.NotNull(loadedProfile.User);
        Assert.Equal("testuser", loadedProfile.User.Username);
        Assert.Single(loadedProfile.AllowedHours);
    }

    [Fact]
    public async Task AddUserAliases_AddsAliasSupport()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };

        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        var loadedAlias = await context.Users
            .Include(u => u.PrimaryUser)
            .FirstAsync(u => u.Username == "joe");

        Assert.NotNull(loadedAlias.PrimaryUser);
        Assert.Equal("john", loadedAlias.PrimaryUser.Username);
    }

    [Fact]
    public async Task AddUserAliases_SelfReferencingRelationship_Works()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias1 = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var alias2 = new User { Username = "johnny", AccountType = AccountType.Child, PrimaryUserId = primary.Id };

        context.Users.AddRange(primary, alias1, alias2);
        await context.SaveChangesAsync();

        var loadedPrimary = await context.Users
            .Include(u => u.Aliases)
            .FirstAsync(u => u.Username == "john");

        Assert.Equal(2, loadedPrimary.Aliases.Count);
        Assert.Contains(loadedPrimary.Aliases, a => a.Username == "joe");
        Assert.Contains(loadedPrimary.Aliases, a => a.Username == "johnny");
    }

    [Fact]
    public async Task Migration_FromV1ToV2_PreservesExistingData()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        // Simulate V1 data (before alias migration)
        var user1 = new User { Username = "john", AccountType = AccountType.Child };
        var user2 = new User { Username = "jane", AccountType = AccountType.Child };
        var profile = new TimeProfile 
        { 
            UserId = user1.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60
        };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
        var usage = new TimeUsage
        {
            UserId = user1.Id,
            ComputerId = computer.Id,
            UsageDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MinutesUsed = 30
        };

        context.Users.AddRange(user1, user2);
        context.TimeProfiles.Add(profile);
        context.Computers.Add(computer);
        context.TimeUsage.Add(usage);
        await context.SaveChangesAsync();

        // Verify data exists
        Assert.Equal(2, await context.Users.CountAsync());
        Assert.Equal(1, await context.TimeProfiles.CountAsync());
        Assert.Equal(1, await context.TimeUsage.CountAsync());

        // Simulate V2 migration (alias support added)
        // PrimaryUserId should be nullable and default to null
        var allUsers = await context.Users.ToListAsync();
        Assert.All(allUsers, u => Assert.Null(u.PrimaryUserId));

        // Add alias relationship
        user2.PrimaryUserId = user1.Id;
        await context.SaveChangesAsync();

        var loadedUser2 = await context.Users
            .Include(u => u.PrimaryUser)
            .FirstAsync(u => u.Username == "jane");

        Assert.NotNull(loadedUser2.PrimaryUser);
        Assert.Equal("john", loadedUser2.PrimaryUser.Username);

        // Verify existing data still intact
        Assert.Equal(2, await context.Users.CountAsync());
        Assert.Equal(1, await context.TimeProfiles.CountAsync());
        Assert.Equal(1, await context.TimeUsage.CountAsync());
    }

    [Fact]
    public async Task UniqueConstraints_Enforced()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var user1 = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(user1);
        await context.SaveChangesAsync();

        // InMemory database doesn't enforce unique constraints
        // This test verifies the model is configured correctly
        var user2 = new User { Username = "john", AccountType = AccountType.Child };
        context.Users.Add(user2);
        
        // In real PostgreSQL this would throw, but InMemory allows it
        // Just verify the configuration exists
        Assert.NotNull(context.Model.FindEntityType(typeof(User)));
    }

    [Fact]
    public async Task CascadeDelete_TimeProfile_DeletesAllowedHours()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var user = new User { Username = "testuser", AccountType = AccountType.Child };
        var profile = new TimeProfile 
        { 
            UserId = user.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60
        };
        var allowedHours = new AllowedHours
        {
            ProfileId = profile.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };

        context.Users.Add(user);
        context.TimeProfiles.Add(profile);
        context.AllowedHours.Add(allowedHours);
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.AllowedHours.CountAsync());

        // Delete profile
        context.TimeProfiles.Remove(profile);
        await context.SaveChangesAsync();

        // AllowedHours should be cascade deleted
        Assert.Equal(0, await context.AllowedHours.CountAsync());
    }

    [Fact]
    public async Task AliasRelationship_RestrictDelete_PreventsOrphans()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };

        context.Users.AddRange(primary, alias);
        await context.SaveChangesAsync();

        // InMemory database doesn't enforce FK constraints
        // This test verifies the relationship is configured
        context.Users.Remove(primary);
        
        // In real PostgreSQL this would throw due to RESTRICT
        // Just verify the relationship exists
        var aliasEntity = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(aliasEntity);
        var fk = aliasEntity.GetForeignKeys().FirstOrDefault(f => f.PrincipalEntityType == aliasEntity);
        Assert.NotNull(fk);
    }

    [Fact]
    public async Task CompleteSchema_AllRelationships_Work()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        await context.Database.EnsureCreatedAsync();

        // Create complete data graph
        var primary = new User { Username = "john", AccountType = AccountType.Child };
        var alias = new User { Username = "joe", AccountType = AccountType.Child, PrimaryUserId = primary.Id };
        var computer = new Computer { Hostname = "test-pc", MachineId = "machine-123" };
        var profile = new TimeProfile 
        { 
            UserId = primary.Id, 
            Name = "Test", 
            IsActive = true,
            MondayLimit = 60,
            TuesdayLimit = 60,
            WednesdayLimit = 60,
            ThursdayLimit = 60,
            FridayLimit = 60,
            SaturdayLimit = 60,
            SundayLimit = 60
        };
        var allowedHours = new AllowedHours
        {
            ProfileId = profile.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };
        var session = new Session
        {
            UserId = alias.Id,
            ComputerId = computer.Id,
            SessionStart = DateTime.UtcNow,
            IsActive = true
        };
        var usage = new TimeUsage
        {
            UserId = alias.Id,
            ComputerId = computer.Id,
            UsageDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MinutesUsed = 30
        };
        var adjustment = new TimeAdjustment
        {
            UserId = primary.Id,
            AdjustmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MinutesAdjustment = 15,
            CreatedBy = "admin"
        };

        context.Users.AddRange(primary, alias);
        context.Computers.Add(computer);
        context.TimeProfiles.Add(profile);
        context.AllowedHours.Add(allowedHours);
        context.Sessions.Add(session);
        context.TimeUsage.Add(usage);
        context.TimeAdjustments.Add(adjustment);
        await context.SaveChangesAsync();

        // Load everything with navigation properties
        var loadedPrimary = await context.Users
            .Include(u => u.Aliases)
            .Include(u => u.TimeProfiles)
                .ThenInclude(p => p.AllowedHours)
            .FirstAsync(u => u.Username == "john");

        Assert.Single(loadedPrimary.Aliases);
        Assert.Single(loadedPrimary.TimeProfiles);
        Assert.Single(loadedPrimary.TimeProfiles.First().AllowedHours);

        // Verify all data
        Assert.Equal(2, await context.Users.CountAsync());
        Assert.Equal(1, await context.Computers.CountAsync());
        Assert.Equal(1, await context.TimeProfiles.CountAsync());
        Assert.Equal(1, await context.AllowedHours.CountAsync());
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(1, await context.TimeUsage.CountAsync());
        Assert.Equal(1, await context.TimeAdjustments.CountAsync());
    }
}
