using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParentalControl.Client.Services;
using ParentalControl.Shared.DTOs;
using Xunit;
using UsageRecord = ParentalControl.Client.Services.UsageRecord;

namespace ParentalControl.Client.Tests;

public class ServerSyncServiceTests
{
    private class FakeServerHandler : HttpMessageHandler
    {
        public List<UsageReportRequest> UsageRequests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/client/register")
            {
                var registerResponse = new RegisterComputerResponse(Guid.NewGuid(), "fake-api-key");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(registerResponse) };
            }

            if (request.RequestUri!.AbsolutePath == "/api/client/usage")
            {
                var body = (await request.Content!.ReadFromJsonAsync<UsageReportRequest>(cancellationToken))!;
                UsageRequests.Add(body);

                var usageResponse = new UsageReportResponse(100 - body.MinutesActive, false, "logout", new[] { 15, 10, 5, 1 });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(usageResponse) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static (ServerSyncService service, FakeServerHandler handler) CreateRegisteredService(Mock<ILocalCache> cache)
    {
        var handler = new FakeServerHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server.invalid") };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ParentalControl:ServerUrl"] = "http://server.invalid" })
            .Build();

        var service = new ServerSyncService(httpClient, configuration, NullLogger<ServerSyncService>.Instance, cache.Object);
        return (service, handler);
    }

    [Fact]
    public async Task SubmitUsageAsync_RecordsSpanningTwoDates_SendsOneRequestPerDate()
    {
        // Regression test for #12: batching an offline stretch that spans local midnight
        // must not stamp every record with the first record's date -- otherwise today's
        // minutes get silently booked onto yesterday.
        var cache = new Mock<ILocalCache>();
        var (service, handler) = CreateRegisteredService(cache);

        var registered = await service.RegisterComputerAsync();
        Assert.True(registered);

        var yesterday = DateTime.UtcNow.Date.AddHours(-2); // yesterday evening (UTC)
        var today = DateTime.UtcNow.Date.AddHours(1); // today, just after UTC midnight

        var records = new List<UsageRecord>
        {
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 10, 0, yesterday, false),
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 5, 0, yesterday.AddMinutes(30), false),
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 20, 0, today, false),
        };

        var response = await service.SubmitUsageAsync(records);

        Assert.NotNull(response);
        Assert.Equal(2, handler.UsageRequests.Count);

        var yesterdayRequest = handler.UsageRequests.Single(r => DateOnly.FromDateTime(r.Timestamp) == DateOnly.FromDateTime(yesterday));
        var todayRequest = handler.UsageRequests.Single(r => DateOnly.FromDateTime(r.Timestamp) == DateOnly.FromDateTime(today));

        Assert.Equal(15, yesterdayRequest.MinutesActive); // 10 + 5, not merged with today's 20
        Assert.Equal(20, todayRequest.MinutesActive);

        // The response returned for the live enforcement decision must reflect the most
        // recent (today's) group, not an earlier historical day.
        Assert.Equal(100 - 20, response!.TimeRemainingMinutes);

        cache.Verify(c => c.SaveLastKnownLimitsAsync("child1", It.IsAny<UsageReportResponse>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitUsageAsync_AllRecordsSameDate_SendsSingleRequest()
    {
        var cache = new Mock<ILocalCache>();
        var (service, handler) = CreateRegisteredService(cache);
        await service.RegisterComputerAsync();

        var now = DateTime.UtcNow;
        var records = new List<UsageRecord>
        {
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 1, 0, now, false),
            new(Guid.NewGuid(), Guid.Empty, "child1", Guid.NewGuid(), 1, 0, now.AddMinutes(1), false),
        };

        var response = await service.SubmitUsageAsync(records);

        Assert.NotNull(response);
        Assert.Single(handler.UsageRequests);
        Assert.Equal(2, handler.UsageRequests[0].MinutesActive);
    }
}
