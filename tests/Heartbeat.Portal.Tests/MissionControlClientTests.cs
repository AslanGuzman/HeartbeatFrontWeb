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

    [TestMethod]
    public async Task VitalsEndpointsSendFiltersAndDeserializeContractData()
    {
        var now = DateTimeOffset.UtcNow;
        var astronautId = Guid.NewGuid();
        var checkUpId = Guid.NewGuid();
        var requests = new List<(string Uri, string? Token)>();

        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            requests.Add((request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            var path = request.RequestUri.AbsolutePath;
            HttpContent content;

            if (path.EndsWith("/status", StringComparison.Ordinal))
            {
                content = JsonContent.Create(new
                {
                    astronautId,
                    overallStatus = "watch",
                    lastSyncAt = now,
                    indicators = new[]
                    {
                        new
                        {
                            indicator = "heartRate",
                            value = 64m,
                            secondaryValue = (decimal?)null,
                            unit = "bpm",
                            status = "normal",
                            label = "Normal",
                            recordedAt = now,
                            checkUpId
                        }
                    }
                });
            }
            else if (path.EndsWith("/checkups", StringComparison.Ordinal))
            {
                content = JsonContent.Create(new
                {
                    items = new[]
                    {
                        new
                        {
                            id = checkUpId,
                            recordedAt = now,
                            receivedAt = now,
                            overallStatus = "watch",
                            rangesVersion = "2026.11.14-1",
                            note = (string?)null,
                            supersedesId = (Guid?)null,
                            readings = new[]
                            {
                                new
                                {
                                    id = Guid.NewGuid(),
                                    indicator = "heartRate",
                                    value = 64m,
                                    secondaryValue = (decimal?)null,
                                    status = "normal",
                                    bandIds = new[] { "hr-normal" }
                                }
                            }
                        }
                    },
                    page = 2,
                    pageSize = 20,
                    total = 21
                });
            }
            else if (path.Contains("/checkups/", StringComparison.Ordinal))
            {
                content = JsonContent.Create(new
                {
                    id = checkUpId,
                    recordedAt = now,
                    receivedAt = now,
                    overallStatus = "watch",
                    rangesVersion = "2026.11.14-1",
                    note = (string?)null,
                    supersedesId = (Guid?)null,
                    readings = Array.Empty<object>(),
                    evaluations = new[]
                    {
                        new
                        {
                            indicator = "heartRate",
                            status = "watch",
                            bandIds = new[] { "hr-watch-high" },
                            label = "Fast",
                            guidance = new[] { "Rest and measure again." },
                            escalation = (object?)null,
                            sources = new[]
                            {
                                new
                                {
                                    title = "Pulse",
                                    url = "https://example.test/pulse"
                                }
                            }
                        }
                    }
                });
            }
            else
            {
                content = new StringContent(ReferenceRanges.Json, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var session = new MissionControlSession();
        session.SetTokens("access-token", now.AddHours(1), "refresh-token", now.AddHours(8));
        session.SetUser(new PortalUserDto(
            Guid.NewGuid(),
            "surgeon",
            "surgeon@heartbeat.test",
            "Flight Surgeon",
            new RoleDto(Guid.NewGuid(), "Flight Surgeon", true, 1, [Permissions.VitalsView, Permissions.VitalsHistory]),
            true,
            [Permissions.VitalsView, Permissions.VitalsHistory]));
        var refresher = new MissionControlTokenRefresher(httpClient, session, new StubSessionStore());
        var client = new MissionControlClient(httpClient, refresher);

        var status = await client.GetAstronautStatusAsync(astronautId);
        var history = await client.GetCheckUpsAsync(
            astronautId,
            new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 14, 23, 59, 59, TimeSpan.Zero),
            HealthStatus.Watch,
            page: 2,
            pageSize: 20);
        var detail = await client.GetCheckUpDetailAsync(astronautId, checkUpId);
        using var ranges = await client.GetReferenceRangesAsync();

        Assert.AreEqual(HealthStatus.Watch, status.OverallStatus);
        Assert.AreEqual(HealthStatus.Normal, status.Indicators[0].Status);
        Assert.AreEqual(2, history.Page);
        Assert.AreEqual(21, history.Total);
        Assert.AreEqual(HealthStatus.Watch, detail.Evaluations[0].Status);
        Assert.AreEqual("Rest and measure again.", detail.Evaluations[0].Guidance[0]);
        Assert.AreEqual("2026.11.14-1", ranges.RootElement.GetProperty("version").GetString());
        Assert.IsTrue(requests[1].Uri.Contains("page=2", StringComparison.Ordinal));
        Assert.IsTrue(requests[1].Uri.Contains("status=watch", StringComparison.Ordinal));
        Assert.IsTrue(requests[1].Uri.Contains("from=", StringComparison.Ordinal));
        Assert.IsTrue(requests.All(request => request.Token == "access-token"));
        Assert.HasCount(4, requests);
    }

    [TestMethod]
    public async Task AlertEndpointsFilterPagesAndPostAcknowledgementNote()
    {
        var now = DateTimeOffset.UtcNow;
        var alertId = Guid.NewGuid();
        var astronautId = Guid.NewGuid();
        var checkUpId = Guid.NewGuid();
        var requests = new List<(string Uri, string? Token, string? Body)>();

        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add((
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Parameter,
                body));

            object response = request.Method == HttpMethod.Post
                ? new
                {
                    id = alertId,
                    astronautId,
                    astronautName = "Ana Rivera",
                    checkUpId,
                    indicators = new[] { "bloodPressure" },
                    createdAt = now,
                    acknowledgedAt = now,
                    acknowledgedBy = "Flight Surgeon",
                    acknowledgeNote = "Reviewed with the crew."
                }
                : new
                {
                    items = new[]
                    {
                        new
                        {
                            id = alertId,
                            astronautId,
                            astronautName = "Ana Rivera",
                            checkUpId,
                            indicators = new[] { "bloodPressure" },
                            createdAt = now,
                            acknowledgedAt = (DateTimeOffset?)null,
                            acknowledgedBy = (string?)null,
                            acknowledgeNote = (string?)null
                        }
                    },
                    page = 2,
                    pageSize = 10,
                    total = 11
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            };
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var permissions = new[] { Permissions.AlertsView, Permissions.AlertsAcknowledge };
        var session = new MissionControlSession();
        session.SetTokens("access-token", now.AddHours(1), "refresh-token", now.AddHours(8));
        session.SetUser(new PortalUserDto(
            Guid.NewGuid(),
            "surgeon",
            "surgeon@heartbeat.test",
            "Flight Surgeon",
            new RoleDto(Guid.NewGuid(), "Flight Surgeon", true, 1, permissions),
            true,
            permissions));
        var refresher = new MissionControlTokenRefresher(httpClient, session, new StubSessionStore());
        var client = new MissionControlClient(httpClient, refresher);

        var alerts = await client.GetAlertsAsync("all", page: 2, pageSize: 10);
        var acknowledged = await client.AcknowledgeAlertAsync(alertId, "Reviewed with the crew.");

        Assert.AreEqual(11, alerts.Total);
        Assert.AreEqual(IndicatorType.BloodPressure, alerts.Items[0].Indicators[0]);
        Assert.AreEqual("Flight Surgeon", acknowledged.AcknowledgedBy);
        Assert.AreEqual("/api/mc/v1/alerts?state=all&page=2&pageSize=10", requests[0].Uri);
        Assert.AreEqual($"/api/mc/v1/alerts/{alertId:D}/acknowledge", requests[1].Uri);
        using var acknowledgeBody = JsonDocument.Parse(requests[1].Body!);
        Assert.AreEqual(
            "Reviewed with the crew.",
            acknowledgeBody.RootElement.GetProperty("note").GetString());
        Assert.IsTrue(requests.All(request => request.Token == "access-token"));
    }

    [TestMethod]
    public async Task AlertCounterRefreshesTheOpenAlertTotal()
    {
        var now = DateTimeOffset.UtcNow;
        string? requestedUri = null;

        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            requestedUri = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    items = Array.Empty<object>(),
                    page = 1,
                    pageSize = 1,
                    total = 4
                })
            });
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var session = new MissionControlSession();
        session.SetTokens("access-token", now.AddHours(1), "refresh-token", now.AddHours(8));
        var refresher = new MissionControlTokenRefresher(httpClient, session, new StubSessionStore());
        var counter = new AlertCounterState(new MissionControlClient(httpClient, refresher));
        var changeCount = 0;
        counter.Changed += () => changeCount++;

        await counter.RefreshAsync();

        Assert.AreEqual(4, counter.OpenCount);
        Assert.AreEqual(1, changeCount);
        Assert.AreEqual("/api/mc/v1/alerts?state=open&page=1&pageSize=1", requestedUri);
    }

    [TestMethod]
    public async Task SetUserRoleSendsTheContractRequestAndReadsTheUpdatedUser()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Body)>();

        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requests.Add((
                request.Method.Method,
                request.RequestUri!.AbsolutePath,
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = userId,
                    userName = "a.rivera",
                    email = "ana@heartbeat.test",
                    displayName = "Ana Rivera",
                    role = new
                    {
                        id = roleId,
                        name = "Flight Surgeon",
                        isSystem = false,
                        userCount = 1,
                        permissions = new[] { Permissions.VitalsView }
                    },
                    isActive = true,
                    permissions = new[] { Permissions.VitalsView }
                })
            };
        }))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var session = new MissionControlSession();
        session.SetTokens("access-token", now.AddHours(1), "refresh-token", now.AddHours(8));
        var refresher = new MissionControlTokenRefresher(httpClient, session, new StubSessionStore());
        var client = new MissionControlClient(httpClient, refresher);

        var user = await client.SetUserRoleAsync(userId, roleId);

        Assert.AreEqual(roleId, user.Role.Id);
        Assert.HasCount(1, requests);
        Assert.AreEqual("PUT", requests[0].Method);
        Assert.AreEqual($"/api/mc/v1/users/{userId:D}/role", requests[0].Path);
        using var body = JsonDocument.Parse(requests[0].Body!);
        Assert.AreEqual(roleId, body.RootElement.GetProperty("roleId").GetGuid());
    }

    [TestMethod]
    public async Task ApiValidationErrorsAreExposedInTheExceptionMessage()
    {
        var now = DateTimeOffset.UtcNow;

        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"title":"One or more validation errors occurred.","errors":{"email":["A valid email address is required."]}}""",
                    System.Text.Encoding.UTF8,
                    "application/problem+json")
            })))
        {
            BaseAddress = new Uri("https://mission-control.test/api/mc/v1/")
        };

        var session = new MissionControlSession();
        session.SetTokens("access-token", now.AddHours(1), "refresh-token", now.AddHours(8));
        var refresher = new MissionControlTokenRefresher(httpClient, session, new StubSessionStore());
        var client = new MissionControlClient(httpClient, refresher);

        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            client.CreateUserAsync(new CreateUserRequest(
                "a.rivera",
                "not-an-email",
                "Ana Rivera",
                "Password1!",
                Guid.NewGuid())));

        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.IsTrue(
            exception.Message.Contains("A valid email address is required.", StringComparison.Ordinal),
            exception.Message);
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
