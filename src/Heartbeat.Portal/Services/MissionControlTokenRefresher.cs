using System.Net;
using System.Net.Http.Json;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

public sealed class MissionControlTokenRefresher(
    HttpClient httpClient,
    MissionControlSession session)
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
                return session.AccessTokenExpiresAt is { } expiresAt && expiresAt > now
                    ? session.AccessToken
                    : null;
            }

            if (session.RefreshTokenExpiresAt is { } refreshExpiresAt && refreshExpiresAt <= now)
            {
                session.Clear();
                return null;
            }

            using var response = await httpClient.PostAsJsonAsync(
                "auth/refresh",
                new RefreshRequest(refreshToken),
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                session.Clear();
                return null;
            }

            response.EnsureSuccessStatusCode();
            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);

            if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                tokens.AccessTokenExpiresAt <= now)
            {
                session.Clear();
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

            return tokens.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
