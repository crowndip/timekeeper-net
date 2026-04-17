using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Controllers;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class ComputersControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ComputersController _controller;

    public ComputersControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _controller = new ComputersController(_context);
    }

    [Fact]
    public async Task GetComputers_ReturnsAllComputers()
    {
        // Arrange
        _context.Computers.AddRange(
            new Computer { Hostname = "pc1", MachineId = "id1" },
            new Computer { Hostname = "pc2", MachineId = "id2" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetComputers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetComputer_WithValidId_ReturnsComputer()
    {
        // Arrange
        var computer = new Computer { Hostname = "testpc", MachineId = "testid" };
        _context.Computers.Add(computer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetComputer(computer.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetComputer_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetComputer(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteComputer_WithValidId_DeletesComputer()
    {
        // Arrange
        var computer = new Computer { Hostname = "testpc", MachineId = "testid" };
        _context.Computers.Add(computer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteComputer(computer.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, await _context.Computers.CountAsync());
    }

    [Fact]
    public async Task ToggleActive_TogglesComputerStatus()
    {
        // Arrange
        var computer = new Computer { Hostname = "testpc", MachineId = "testid", IsActive = true };
        _context.Computers.Add(computer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.ToggleActive(computer.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = await _context.Computers.FindAsync(computer.Id);
        Assert.False(updated?.IsActive);
    }

    [Fact]
    public async Task RegenerateApiKey_GeneratesNewKey()
    {
        // Arrange
        var computer = new Computer { Hostname = "testpc", MachineId = "testid", ApiKey = "oldkey" };
        _context.Computers.Add(computer);
        await _context.SaveChangesAsync();
        var oldKey = computer.ApiKey;

        // Act
        var result = await _controller.RegenerateApiKey(computer.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = await _context.Computers.FindAsync(computer.Id);
        Assert.NotEqual(oldKey, updated?.ApiKey);
        Assert.NotNull(updated?.ApiKey);
    }

    [Fact]
    public async Task RegenerateApiKey_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.RegenerateApiKey(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
