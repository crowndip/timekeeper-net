using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ParentalControl.WebService.Services;
using Xunit;

namespace ParentalControl.WebService.Tests;

public class AuthServiceTests
{
    // The failure tracker is a static dictionary keyed by IP (a single household admin
    // password shared across the whole process), so each test uses its own unique fake
    // "IP" (a fresh Guid) to stay isolated from every other test in the run.
    private static string FreshIp() => Guid.NewGuid().ToString();

    private static AuthService CreateService(string password = "correct-password")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LimitAdministratorPassword"] = password })
            .Build();
        return new AuthService(configuration, new HttpContextAccessor());
    }

    [Fact]
    public void ValidatePassword_CorrectPassword_ReturnsTrue()
    {
        var service = CreateService("hunter2");
        Assert.True(service.ValidatePassword("hunter2"));
    }

    [Fact]
    public void ValidatePassword_WrongPassword_ReturnsFalse()
    {
        var service = CreateService("hunter2");
        Assert.False(service.ValidatePassword("wrong"));
    }

    [Fact]
    public void ValidatePassword_EmptyPassword_ReturnsFalse()
    {
        var service = CreateService("hunter2");
        Assert.False(service.ValidatePassword(""));
    }

    [Fact]
    public void IsLockedOut_BeforeAnyFailures_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsLockedOut(FreshIp(), out _));
    }

    [Fact]
    public void IsLockedOut_AfterFiveFailures_ReturnsTrue()
    {
        var service = CreateService();
        var ip = FreshIp();

        for (var i = 0; i < 5; i++)
            service.RecordLoginFailure(ip);

        Assert.True(service.IsLockedOut(ip, out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
        Assert.True(retryAfter <= TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void IsLockedOut_AfterFourFailures_StillAllowed()
    {
        var service = CreateService();
        var ip = FreshIp();

        for (var i = 0; i < 4; i++)
            service.RecordLoginFailure(ip);

        Assert.False(service.IsLockedOut(ip, out _));
    }

    [Fact]
    public void RecordLoginSuccess_ClearsFailureCount()
    {
        var service = CreateService();
        var ip = FreshIp();

        for (var i = 0; i < 4; i++)
            service.RecordLoginFailure(ip);

        service.RecordLoginSuccess(ip);

        // Failure count reset -- four more failures alone shouldn't trigger a lockout.
        for (var i = 0; i < 4; i++)
            service.RecordLoginFailure(ip);

        Assert.False(service.IsLockedOut(ip, out _));
    }

    [Fact]
    public void DifferentIps_AreThrottledIndependently()
    {
        var service = CreateService();
        var ip1 = FreshIp();
        var ip2 = FreshIp();

        for (var i = 0; i < 5; i++)
            service.RecordLoginFailure(ip1);

        Assert.True(service.IsLockedOut(ip1, out _));
        Assert.False(service.IsLockedOut(ip2, out _));
    }
}
