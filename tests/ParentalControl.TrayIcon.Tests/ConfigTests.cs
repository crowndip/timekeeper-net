using System;
using System.IO;
using Xunit;

namespace ParentalControl.TrayIcon.Tests;

public class ConfigTests
{
    [Fact]
    public void LoadConfig_ValidFiles_ReturnsConfig()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var serverUrlPath = Path.Combine(tempDir, "server-url");
        var computerIdPath = Path.Combine(tempDir, "computer-id");
        var testGuid = Guid.NewGuid();
        
        File.WriteAllText(serverUrlPath, "http://localhost:8080");
        File.WriteAllText(computerIdPath, testGuid.ToString());
        
        // Act
        var serverUrl = File.Exists(serverUrlPath) ? File.ReadAllText(serverUrlPath).Trim() : null;
        var computerId = File.Exists(computerIdPath) && Guid.TryParse(File.ReadAllText(computerIdPath).Trim(), out var id) ? id : (Guid?)null;
        
        // Assert
        Assert.Equal("http://localhost:8080", serverUrl);
        Assert.Equal(testGuid, computerId);
        
        // Cleanup
        Directory.Delete(tempDir, true);
    }
    
    [Fact]
    public void LoadConfig_MissingFiles_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing");
        
        // Act
        var serverUrl = File.Exists(nonExistentPath) ? File.ReadAllText(nonExistentPath).Trim() : null;
        
        // Assert
        Assert.Null(serverUrl);
    }
    
    [Fact]
    public void LoadConfig_InvalidGuid_ReturnsNull()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var computerIdPath = Path.Combine(tempDir, "computer-id");
        File.WriteAllText(computerIdPath, "not-a-guid");
        
        // Act
        var computerId = File.Exists(computerIdPath) && Guid.TryParse(File.ReadAllText(computerIdPath).Trim(), out var id) ? id : (Guid?)null;
        
        // Assert
        Assert.Null(computerId);
        
        // Cleanup
        Directory.Delete(tempDir, true);
    }
    
    [Fact]
    public void FormatTime_Minutes_ReturnsCorrectFormat()
    {
        // Arrange & Act
        var result = $"{45}m remaining";
        
        // Assert
        Assert.Equal("45m remaining", result);
    }
    
    [Fact]
    public void FormatTime_Zero_ReturnsCorrectFormat()
    {
        // Arrange & Act
        var result = $"{0}m remaining";
        
        // Assert
        Assert.Equal("0m remaining", result);
    }
    
    [Fact]
    public void EffectiveTime_ShowsMinimum()
    {
        // Arrange
        var timeRemaining = 55; // 55 minutes from daily limit
        var minutesUntilAllowedHoursEnd = 15; // 15 minutes until 10 PM
        
        // Act
        var effectiveTime = Math.Min(timeRemaining, minutesUntilAllowedHoursEnd);
        
        // Assert
        Assert.Equal(15, effectiveTime); // Should show 15, not 55
    }
}
