using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

public sealed class MissionControlClient(
    HttpClient httpClient,
    MissionControlTokenRefresher tokenRefresher)
{
    private static readonly JsonSerializerOptions MissionControlJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessToken = await tokenRefresher.GetAccessTokenAsync(cancellationToken);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await tokenRefresher.ClearSessionAsync(cancellationToken);
        }

        return response;
    }

    public Task<HttpResponseMessage> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default) =>
        httpClient.PostAsJsonAsync("auth/login", request, cancellationToken);

    public async Task<HttpResponseMessage> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        return await SendAsync(request, cancellationToken);
    }

    public Task<CrewSummaryDto[]> GetCrewAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CrewSummaryDto[]>("crew", cancellationToken);

    public Task<AstronautProfile> GetCrewMemberAsync(
        Guid astronautId,
        CancellationToken cancellationToken = default) =>
        GetAsync<AstronautProfile>($"crew/{astronautId:D}", cancellationToken);

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(
            MissionControlJsonOptions,
            cancellationToken) ?? throw new InvalidDataException(
                "The Mission Control API returned an empty response.");
    }
}
