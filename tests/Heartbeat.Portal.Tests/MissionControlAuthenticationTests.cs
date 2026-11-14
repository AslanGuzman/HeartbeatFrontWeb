using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Portal.Services;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Tests;

[TestClass]
public sealed class MissionControlAuthenticationTests
{
    [TestMethod]
    public async Task Login_PostsCredentialsThenLoadsCurrentUserAndPermissions()
    {
        var now = DateTimeOffset.UtcNow;
        var expectedUser = User([Permissions.CrewView, Permissions.VitalsView]);
        var session = new MissionControlSession();
        var store = new StubSessionStore(session);
        var calls = new List<string>();

        using var httpClient = CreateClient((request, cancellationToken) =>
        {
            calls.Add(request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Post)
            {
                Assert.AreEqual("/api/mc/v1/auth/login", request.RequestUri.AbsolutePath);
                return Task.FromResult(JsonResponse(new McLoginResponse(
                    "access-token",
                    now.AddHours(1),
                    "refresh-token",
                    now.AddHours(8),
                    expectedUser)));
            }

            Assert.AreEqual("/api/mc/v1/me", request.RequestUri.AbsolutePath);
            Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
            Assert.AreEqual("access-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(JsonResponse(expectedUser));
        });
        var auth = CreateAuthentication(httpClient, session, store);

        var result = await auth.LoginAsync("pilot", "secret");

        Assert.AreEqual(MissionControlLoginResult.Success, result);
        CollectionAssert.AreEqual(
            new[] { "/api/mc/v1/auth/login", "/api/mc/v1/me" },
            calls.ToArray());
        Assert.AreEqual("pilot", session.User?.UserName);
        Assert.IsTrue(session.HasPermission(Permissions.CrewView));
        Assert.IsTrue(session.HasPermission(Permissions.VitalsView));
        Assert.AreEqual(1, store.SaveCalls);
    }

    [TestMethod]
    public async Task Login_WithUnauthorizedCredentialsDoesNotCreateASession()
    {
        var session = new MissionControlSession();
        var store = new StubSessionStore(session);
        var meCalls = 0;

        using var httpClient = CreateClient((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                meCalls++;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
        var auth = CreateAuthentication(httpClient, session, store);

        var result = await auth.LoginAsync("pilot", "wrong");

        Assert.AreEqual(MissionControlLoginResult.InvalidCredentials, result);
        Assert.IsNull(session.AccessToken);
        Assert.IsNull(session.User);
        Assert.AreEqual(0, meCalls);
        Assert.AreEqual(0, store.SaveCalls);
    }

    [TestMethod]
    public async Task LogoutClearsTokensUserPermissionsAndPersistedSession()
    {
        var session = new MissionControlSession();
        session.SetTokens(
            "access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            DateTimeOffset.UtcNow.AddHours(8));
        session.SetUser(User([Permissions.CrewView]));
        var store = new StubSessionStore(session);
        using var httpClient = CreateClient((_, _) =>
            throw new AssertFailedException("Logout must not call an undocumented API route."));
        var auth = CreateAuthentication(httpClient, session, store);

        await auth.LogoutAsync();

        Assert.IsNull(session.AccessToken);
        Assert.IsNull(session.RefreshToken);
        Assert.IsNull(session.User);
        Assert.IsFalse(session.HasPermission(Permissions.CrewView));
        Assert.AreEqual(1, store.ClearCalls);
    }

    [TestMethod]
    public async Task UnauthorizedResponseClearsTheSession()
    {
        var session = AuthenticatedSession();
        var store = new StubSessionStore(session);
        using var httpClient = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var client = CreateMissionControlClient(httpClient, session, store);

        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "crew"));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsNull(session.User);
        Assert.IsNull(session.AccessToken);
        Assert.IsTrue(session.SessionExpired);
        Assert.AreEqual(1, store.ClearCalls);
    }

    [TestMethod]
    public async Task ForbiddenResponseKeepsTheAuthenticatedSession()
    {
        var session = AuthenticatedSession();
        var store = new StubSessionStore(session);
        using var httpClient = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var client = CreateMissionControlClient(httpClient, session, store);

        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "crew"));

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsNotNull(session.User);
        Assert.IsNotNull(session.AccessToken);
        Assert.IsFalse(session.SessionExpired);
        Assert.AreEqual(0, store.ClearCalls);
    }

    [TestMethod]
    public async Task RestoreLoadsThePersistedTokensAndRefreshesTheUserFromMe()
    {
        var session = new MissionControlSession();
        var store = new StubSessionStore(session)
        {
            TokensToRestore = new StoredTokens(
                "restored-access-token",
                DateTimeOffset.UtcNow.AddHours(1),
                "restored-refresh-token",
                DateTimeOffset.UtcNow.AddHours(8))
        };
        var user = User([Permissions.AlertsView]);
        using var httpClient = CreateClient((request, _) =>
        {
            Assert.AreEqual("/api/mc/v1/me", request.RequestUri?.AbsolutePath);
            Assert.AreEqual("restored-access-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(JsonResponse(user));
        });
        var auth = CreateAuthentication(httpClient, session, store);

        var restored = await auth.RestoreAsync();

        Assert.IsTrue(restored);
        Assert.AreEqual(user.Id, session.User?.Id);
        Assert.IsTrue(session.HasPermission(Permissions.AlertsView));
        Assert.AreEqual(1, store.RestoreCalls);
    }

    private static MissionControlAuthentication CreateAuthentication(
        HttpClient apiClient,
        MissionControlSession session,
        IMissionControlSessionStore store)
    {
        return new MissionControlAuthentication(
            CreateMissionControlClient(apiClient, session, store),
            session,
            store);
    }

    private static MissionControlClient CreateMissionControlClient(
        HttpClient apiClient,
        MissionControlSession session,
        IMissionControlSessionStore store) =>
        new(apiClient, new MissionControlTokenRefresher(apiClient, session, store));

    private static MissionControlSession AuthenticatedSession()
    {
        var session = new MissionControlSession();
        session.SetTokens(
            "access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            DateTimeOffset.UtcNow.AddHours(8));
        session.SetUser(User([Permissions.CrewView]));
        return session;
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new StubHttpMessageHandler(send))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
    };

    private static PortalUserDto User(IReadOnlyList<string> permissions) => new(
        Guid.NewGuid(),
        "pilot",
        "pilot@heartbeat.test",
        "Pilot",
        new RoleDto(Guid.NewGuid(), "Flight Surgeon", true, 1, permissions),
        true,
        permissions);

    private sealed record StoredTokens(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt);

    private sealed class StubSessionStore(MissionControlSession session) : IMissionControlSessionStore
    {
        public int SaveCalls { get; private set; }
        public int ClearCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public StoredTokens? TokensToRestore { get; init; }

        public Task RestoreAsync(CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            if (TokensToRestore is { } tokens)
            {
                session.SetTokens(
                    tokens.AccessToken,
                    tokens.AccessTokenExpiresAt,
                    tokens.RefreshToken,
                    tokens.RefreshTokenExpiresAt);
            }

            return Task.CompletedTask;
        }

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
