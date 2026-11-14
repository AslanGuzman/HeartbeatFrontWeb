using System.Net;
using System.Net.Http.Json;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

public sealed class MissionControlTokenRefresher(
    HttpClient httpClient,
    MissionControlSession session,
    IMissionControlSessionStore sessionStore)
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (session.AccessToken is { Length: > 0 } accessToken &&
                session.AccessTokenExpiresAt is { } accessExpiresAt &&
                accessExpiresAt > now + RefreshWindow)
            {
                return accessToken;
            }

            var refreshToken = session.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                if (session.AccessTokenExpiresAt is { } expiresAt && expiresAt > now)
                {
                    return session.AccessToken;
                }

                if (session.AccessToken is not null)
                {
                    await ClearSessionAsync(cancellationToken);
                }

                return null;
            }

            if (session.RefreshTokenExpiresAt is { } refreshExpiresAt && refreshExpiresAt <= now)
            {
                await ClearSessionAsync(cancellationToken);
                return null;
            }

            using var response = await httpClient.PostAsJsonAsync(
                "auth/refresh",
                new RefreshRequest(refreshToken),
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.BadRequest or
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
            {
                await ClearSessionAsync(cancellationToken);
                return null;
            }

            response.EnsureSuccessStatusCode();
            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);

            if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                tokens.AccessTokenExpiresAt <= now || tokens.RefreshTokenExpiresAt is { } nextRefreshExpiresAt &&
                nextRefreshExpiresAt <= now)
            {
                await ClearSessionAsync(cancellationToken);
                throw new InvalidDataException("The Mission Control API returned an invalid token response.");
            }

            var nextRefreshToken = string.IsNullOrWhiteSpace(tokens.RefreshToken)
                ? refreshToken
                : tokens.RefreshToken;
            session.UpdateTokens(
                tokens.AccessToken,
                tokens.AccessTokenExpiresAt,
                nextRefreshToken,
                tokens.RefreshTokenExpiresAt ?? session.RefreshTokenExpiresAt);
            await sessionStore.SaveAsync(cancellationToken);

            using var meRequest = new HttpRequestMessage(HttpMethod.Get, "me");
            meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                tokens.AccessToken);
            using var meResponse = await httpClient.SendAsync(meRequest, cancellationToken);
            if (meResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAsync(cancellationToken);
                return null;
            }

            meResponse.EnsureSuccessStatusCode();
            var user = await meResponse.Content.ReadFromJsonAsync<PortalUserDto>(
                cancellationToken: cancellationToken);
            if (user is null || !user.IsActive)
            {
                await ClearSessionAsync(cancellationToken);
                return null;
            }

            session.SetUser(user);

            return tokens.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        session.Clear(expired: true);
        await sessionStore.ClearAsync(cancellationToken);
    }
}
