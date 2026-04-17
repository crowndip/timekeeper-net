using ParentalControl.WebService.Models;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class ValidationHelperTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(720, true)]
    [InlineData(1440, true)]
    [InlineData(-1, false)]
    [InlineData(1441, false)]
    public void ValidateTimeLimit_ReturnsExpected(int minutes, bool expected)
    {
        Assert.Equal(expected, ValidationHelper.ValidateTimeLimit(minutes));
    }

    [Theory]
    [InlineData("john", true)]
    [InlineData("john_doe", true)]
    [InlineData("john-doe", true)]
    [InlineData("john.doe", true)]
    [InlineData("john123", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("john doe", false)] // Space not allowed
    [InlineData("john@doe", false)] // @ not allowed
    public void ValidateUsername_ReturnsExpected(string? username, bool expected)
    {
        Assert.Equal(expected, ValidationHelper.ValidateUsername(username));
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name+tag@example.co.uk", true)]
    [InlineData("", true)] // Empty is valid (optional field)
    [InlineData(null, true)] // Null is valid (optional field)
    [InlineData("invalid", false)]
    [InlineData("@example.com", false)]
    [InlineData("test@", false)]
    public void ValidateEmail_ReturnsExpected(string? email, bool expected)
    {
        Assert.Equal(expected, ValidationHelper.ValidateEmail(email));
    }

    [Theory]
    [InlineData("My Profile", true)]
    [InlineData("A", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateProfileName_ReturnsExpected(string? name, bool expected)
    {
        Assert.Equal(expected, ValidationHelper.ValidateProfileName(name));
    }

    [Fact]
    public void ValidateUsername_TooLong_ReturnsFalse()
    {
        var longUsername = new string('a', 65);
        Assert.False(ValidationHelper.ValidateUsername(longUsername));
    }

    [Fact]
    public void ValidateEmail_TooLong_ReturnsFalse()
    {
        var longEmail = new string('a', 250) + "@test.com";
        Assert.False(ValidationHelper.ValidateEmail(longEmail));
    }

    [Fact]
    public void ValidateProfileName_TooLong_ReturnsFalse()
    {
        var longName = new string('a', 101);
        Assert.False(ValidationHelper.ValidateProfileName(longName));
    }
}
