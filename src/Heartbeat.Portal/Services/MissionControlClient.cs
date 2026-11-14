using System.Net.Http.Headers;

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

        return await httpClient.SendAsync(request, cancellationToken);
    }
}
