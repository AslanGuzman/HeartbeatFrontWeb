using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Portal.Services;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Tests;

[TestClass]
public sealed class MissionControlClientTests
{
    [TestMethod]
    public async Task CrewEndpointsUseTheAuthenticatedRoutesAndReadContractEnums()
    {
        var now = DateTimeOffset.UtcNow;
        var astronautId = Guid.NewGuid();
        var requests = new List<(string Path, string? Token)>();
        var mission = new MissionInfo(Guid.NewGuid(), "Artemis Demo 1", "station", now);

        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            requests.Add((
                request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.Parameter));

            object response = request.RequestUri.AbsolutePath.EndsWith("/crew", StringComparison.Ordinal)
                ? new[]
                {
                    new
                    {
                        AstronautId = astronautId,
                        DisplayName = "Ana Rivera",
                        CallSign = "Comet",
                        OverallStatus = "critical",
                        LastCheckUpAt = now.AddMinutes(-10),
                        LastSyncAt = now.AddMinutes(-5),
                        OpenAlerts = 1,
                        UnreadMessages = 2
                    }
                }
                : new AstronautProfile(
                    astronautId,
                    "a.rivera",
                    "Ana Rivera",
                    "Comet",
                    mission,
                    null,
                    null,
                    now);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    response,
                    options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var session = new MissionControlSession();
        session.SetTokens("access-token", now.AddHours(1), "refresh-token", now.AddHours(8));
        session.SetUser(new PortalUserDto(
            Guid.NewGuid(),
            "operator",
            "operator@heartbeat.test",
            "Operator",
            new RoleDto(Guid.NewGuid(), "Mission Control Operator", true, 1, [Permissions.CrewView]),
            true,
            [Permissions.CrewView]));
        var refresher = new MissionControlTokenRefresher(httpClient, session, new StubSessionStore());
        var client = new MissionControlClient(httpClient, refresher);

        var crew = await client.GetCrewAsync();
        var astronaut = await client.GetCrewMemberAsync(astronautId);

        Assert.HasCount(1, crew);
        Assert.AreEqual(HealthStatus.Critical, crew[0].OverallStatus);
        Assert.AreEqual(1, crew[0].OpenAlerts);
        Assert.AreEqual(2, crew[0].UnreadMessages);
        Assert.AreEqual(astronautId, astronaut.Id);
        CollectionAssert.AreEqual(
            new[] { "/api/mc/v1/crew", $"/api/mc/v1/crew/{astronautId:D}" },
            requests.Select(request => request.Path).ToArray());
        Assert.IsTrue(requests.All(request => request.Token == "access-token"));
    }

    private sealed class StubSessionStore : IMissionControlSessionStore
    {
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
