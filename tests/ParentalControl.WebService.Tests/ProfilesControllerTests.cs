using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class ProfilesControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ProfilesController _controller;
    private readonly Guid _testUserId;

    public ProfilesControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _controller = new ProfilesController(_context);
        
        _testUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _testUserId, Username = "testuser", AccountType = AccountType.Child });
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetProfiles_ReturnsAllProfiles()
    {
        // Arrange
        _context.TimeProfiles.AddRange(
            new TimeProfile { UserId = _testUserId, Name = "Profile1" },
            new TimeProfile { UserId = _testUserId, Name = "Profile2" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetProfiles();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetProfiles_FiltersByUserId()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = otherUserId, Username = "other", AccountType = AccountType.Child });
        _context.TimeProfiles.AddRange(
            new TimeProfile { UserId = _testUserId, Name = "Profile1" },
            new TimeProfile { UserId = otherUserId, Name = "Profile2" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetProfiles(_testUserId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateProfile_WithValidData_CreatesProfile()
    {
        // Arrange
        var profile = new TimeProfile { UserId = _testUserId, Name = "New Profile", MondayLimit = 120 };

        // Act
        var result = await _controller.CreateProfile(profile);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await _context.TimeProfiles.CountAsync());
    }

    [Fact]
    public async Task CreateProfile_WithDuplicateName_ReturnsBadRequest()
    {
        // Arrange
        _context.TimeProfiles.Add(new TimeProfile { UserId = _testUserId, Name = "Existing" });
        await _context.SaveChangesAsync();
        var profile = new TimeProfile { UserId = _testUserId, Name = "Existing" };

        // Act
        var result = await _controller.CreateProfile(profile);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateProfile_ActiveProfile_DeactivatesOthers()
    {
        // Arrange
        var existing = new TimeProfile { UserId = _testUserId, Name = "Existing", IsActive = true };
        _context.TimeProfiles.Add(existing);
        await _context.SaveChangesAsync();
        var newProfile = new TimeProfile { UserId = _testUserId, Name = "New", IsActive = true };

        // Act
        await _controller.CreateProfile(newProfile);

        // Assert
        var existingUpdated = await _context.TimeProfiles.FindAsync(existing.Id);
        Assert.False(existingUpdated?.IsActive);
    }

    [Fact]
    public async Task UpdateProfile_WithValidData_UpdatesProfile()
    {
        // Arrange
        var profile = new TimeProfile { UserId = _testUserId, Name = "Test", MondayLimit = 60 };
        _context.TimeProfiles.Add(profile);
        await _context.SaveChangesAsync();
        profile.MondayLimit = 120;

        // Act
        var result = await _controller.UpdateProfile(profile.Id, profile);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = await _context.TimeProfiles.FindAsync(profile.Id);
        Assert.Equal(120, updated?.MondayLimit);
    }

    [Fact]
    public async Task DeleteProfile_WithValidId_DeletesProfile()
    {
        // Arrange
        var profile = new TimeProfile { UserId = _testUserId, Name = "Test" };
        _context.TimeProfiles.Add(profile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteProfile(profile.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, await _context.TimeProfiles.CountAsync());
    }

    [Fact]
    public async Task ActivateProfile_DeactivatesOtherProfiles()
    {
        // Arrange
        var profile1 = new TimeProfile { UserId = _testUserId, Name = "Profile1", IsActive = true };
        var profile2 = new TimeProfile { UserId = _testUserId, Name = "Profile2", IsActive = false };
        _context.TimeProfiles.AddRange(profile1, profile2);
        await _context.SaveChangesAsync();

        // Act
        await _controller.ActivateProfile(profile2.Id);

        // Assert
        var updated1 = await _context.TimeProfiles.FindAsync(profile1.Id);
        var updated2 = await _context.TimeProfiles.FindAsync(profile2.Id);
        Assert.False(updated1?.IsActive);
        Assert.True(updated2?.IsActive);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
