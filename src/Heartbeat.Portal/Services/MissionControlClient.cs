using System.Net.Http.Headers;
using System.Net.Http.Json;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

public sealed class MissionControlClient(
    HttpClient httpClient,
    MissionControlTokenRefresher tokenRefresher)
{
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
}
