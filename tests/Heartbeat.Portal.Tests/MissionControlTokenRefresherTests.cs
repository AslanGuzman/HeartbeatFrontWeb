using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Portal.Services;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Tests;

[TestClass]
public sealed class MissionControlTokenRefresherTests
{
    [TestMethod]
    public async Task GetAccessTokenAsync_RefreshesExpiringTokenAndRetainsPermissions()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new MissionControlSession();
        session.SetSession(
            "old-access-token",
            now.AddSeconds(-1),
            "refresh-token",
            now.AddHours(1),
            [Permissions.CrewView]);

        var refreshCalls = 0;
        var meCalls = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/auth/refresh", StringComparison.Ordinal) == true)
            {
                refreshCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new TokenResponse(
                            "new-access-token",
                            now.AddHours(1),
                            "rotated-refresh-token",
                            now.AddDays(1)),
                        options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
                });
            }

            meCalls++;
            Assert.AreEqual("/api/mc/v1/me", request.RequestUri?.AbsolutePath);
            Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
            Assert.AreEqual("new-access-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    User([Permissions.AlertsView]),
                    options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var store = new StubSessionStore();
        var refresher = new MissionControlTokenRefresher(httpClient, session, store);
        var accessToken = await refresher.GetAccessTokenAsync();

        Assert.AreEqual("new-access-token", accessToken);
        Assert.AreEqual("rotated-refresh-token", session.RefreshToken);
        Assert.IsFalse(session.HasPermission(Permissions.CrewView));
        Assert.IsTrue(session.HasPermission(Permissions.AlertsView));
        Assert.AreEqual(1, refreshCalls);
        Assert.AreEqual(1, meCalls);
        Assert.AreEqual(1, store.SaveCalls);
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WithExpiredRefreshTokenClearsSession()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new MissionControlSession();
        session.SetSession(
            "expired-access-token",
            now.AddMinutes(-5),
            "expired-refresh-token",
            now.AddSeconds(-1),
            [Permissions.CrewView]);

        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new AssertFailedException("An expired refresh token must not be sent.")));
        var store = new StubSessionStore();
        var refresher = new MissionControlTokenRefresher(httpClient, session, store);

        var accessToken = await refresher.GetAccessTokenAsync();

        Assert.IsNull(accessToken);
        Assert.IsNull(session.AccessToken);
        Assert.IsFalse(session.HasPermission(Permissions.CrewView));
        Assert.IsTrue(session.SessionExpired);
        Assert.AreEqual(1, store.ClearCalls);
    }

    private static PortalUserDto User(IReadOnlyList<string> permissions) => new(
        Guid.NewGuid(),
        "pilot",
        "pilot@heartbeat.test",
        "Pilot",
        new RoleDto(Guid.NewGuid(), "Flight Surgeon", true, 1, permissions),
        true,
        permissions);

    private sealed class StubSessionStore : IMissionControlSessionStore
    {
        public int SaveCalls { get; private set; }
        public int ClearCalls { get; private set; }

        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            return Task.CompletedTask;
        }
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
