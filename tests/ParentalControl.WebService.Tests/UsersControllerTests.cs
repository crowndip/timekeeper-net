using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class UsersControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _controller = new UsersController(_context);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        // Arrange
        _context.Users.AddRange(
            new User { Username = "user1", AccountType = AccountType.Child },
            new User { Username = "user2", AccountType = AccountType.Parent }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        Assert.Equal(2, await _context.Users.CountAsync());
    }

    [Fact]
    public async Task GetUsers_FiltersByAccountType()
    {
        // Arrange
        _context.Users.AddRange(
            new User { Username = "child1", AccountType = AccountType.Child },
            new User { Username = "parent1", AccountType = AccountType.Parent }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers(AccountType.Child);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateUser_WithValidData_CreatesUser()
    {
        // Arrange
        var user = new User { Username = "newuser", AccountType = AccountType.Child };

        // Act
        var result = await _controller.CreateUser(user);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await _context.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        _context.Users.Add(new User { Username = "existing", AccountType = AccountType.Child });
        await _context.SaveChangesAsync();
        var user = new User { Username = "existing", AccountType = AccountType.Child };

        // Act
        var result = await _controller.CreateUser(user);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_UpdatesUser()
    {
        // Arrange
        var user = new User { Username = "testuser", FullName = "Old Name", AccountType = AccountType.Child };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        user.FullName = "New Name";

        // Act
        var result = await _controller.UpdateUser(user.Id, user);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = await _context.Users.FindAsync(user.Id);
        Assert.Equal("New Name", updated?.FullName);
    }

    [Fact]
    public async Task UpdateUser_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var user = new User { Username = "testuser", AccountType = AccountType.Child };

        // Act
        var result = await _controller.UpdateUser(Guid.NewGuid(), user);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WithValidId_DeletesUser()
    {
        // Arrange
        var user = new User { Username = "testuser", AccountType = AccountType.Child };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteUser(user.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, await _context.Users.CountAsync());
    }

    [Fact]
    public async Task DeleteUser_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteUser(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ToggleActive_TogglesUserStatus()
    {
        // Arrange
        var user = new User { Username = "testuser", IsActive = true, AccountType = AccountType.Child };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.ToggleActive(user.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = await _context.Users.FindAsync(user.Id);
        Assert.False(updated?.IsActive);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
