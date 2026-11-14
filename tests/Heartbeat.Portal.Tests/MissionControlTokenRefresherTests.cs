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
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            refreshCalls++;
            Assert.AreEqual("/api/mc/v1/auth/refresh", request.RequestUri?.AbsolutePath);
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
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var refresher = new MissionControlTokenRefresher(httpClient, session);
        var accessToken = await refresher.GetAccessTokenAsync();

        Assert.AreEqual("new-access-token", accessToken);
        Assert.AreEqual("rotated-refresh-token", session.RefreshToken);
        Assert.IsTrue(session.HasPermission(Permissions.CrewView));
        Assert.AreEqual(1, refreshCalls);
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
        var refresher = new MissionControlTokenRefresher(httpClient, session);

        var accessToken = await refresher.GetAccessTokenAsync();

        Assert.IsNull(accessToken);
        Assert.IsNull(session.AccessToken);
        Assert.IsFalse(session.HasPermission(Permissions.CrewView));
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
