using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Shared;
using Microsoft.JSInterop;

namespace Heartbeat.Portal.Services;

public enum MissionControlLoginResult
{
    Success,
    InvalidCredentials,
    InactiveAccount,
    Forbidden,
    Unavailable
}

public sealed class MissionControlAuthentication(
    MissionControlClient client,
    MissionControlSession session,
    IMissionControlSessionStore sessionStore)
{
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private bool _restored;

    public async Task<bool> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (session.User is not null)
        {
            return true;
        }

        await _restoreLock.WaitAsync(cancellationToken);
        try
        {
            if (_restored)
            {
                return session.User is not null;
            }

            await sessionStore.RestoreAsync(cancellationToken);
            if (session.AccessToken is null && session.RefreshToken is null)
            {
                _restored = true;
                return false;
            }

            using var response = await client.GetMeAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAsync(expired: true, cancellationToken);
                _restored = true;
                return false;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _restored = true;
                return false;
            }

            response.EnsureSuccessStatusCode();
            var user = await response.Content.ReadFromJsonAsync<PortalUserDto>(
                cancellationToken: cancellationToken);
            if (user is null || !user.IsActive)
            {
                await ClearSessionAsync(expired: true, cancellationToken);
                _restored = true;
                return false;
            }

            session.SetUser(user);
            _restored = true;
            return true;
        }
        catch
        {
            _restored = false;
            throw;
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    public async Task<MissionControlLoginResult> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The shared request also serves crew login; Mission Control ignores DeviceId.
            using var response = await client.LoginAsync(
                new LoginRequest(userName, password, string.Empty),
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return MissionControlLoginResult.InvalidCredentials;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return MissionControlLoginResult.InactiveAccount;
            }

            if (!response.IsSuccessStatusCode)
            {
                return MissionControlLoginResult.Unavailable;
            }

            var tokens = await response.Content.ReadFromJsonAsync<McLoginResponse>(
                cancellationToken: cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                string.IsNullOrWhiteSpace(tokens.RefreshToken) || tokens.AccessTokenExpiresAt <= now ||
                tokens.RefreshTokenExpiresAt <= now)
            {
                return MissionControlLoginResult.Unavailable;
            }

            session.SetTokens(
                tokens.AccessToken,
                tokens.AccessTokenExpiresAt,
                tokens.RefreshToken,
                tokens.RefreshTokenExpiresAt);
            await sessionStore.SaveAsync(cancellationToken);

            using var meResponse = await client.GetMeAsync(cancellationToken);
            if (meResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAsync(expired: true, cancellationToken);
                return MissionControlLoginResult.InactiveAccount;
            }

            if (meResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                await ClearSessionAsync(expired: false, cancellationToken);
                return MissionControlLoginResult.Forbidden;
            }

            if (!meResponse.IsSuccessStatusCode)
            {
                await ClearSessionAsync(expired: false, cancellationToken);
                return MissionControlLoginResult.Unavailable;
            }

            var user = await meResponse.Content.ReadFromJsonAsync<PortalUserDto>(
                cancellationToken: cancellationToken);
            if (user is null || !user.IsActive)
            {
                await ClearSessionAsync(expired: true, cancellationToken);
                return MissionControlLoginResult.InactiveAccount;
            }

            session.SetUser(user);
            _restored = true;
            return MissionControlLoginResult.Success;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidDataException or JSException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            await ClearSessionAsync(expired: false, cancellationToken);
            return MissionControlLoginResult.Unavailable;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        session.Clear();
        await sessionStore.ClearAsync(cancellationToken);
        _restored = true;
    }

    private async Task ClearSessionAsync(bool expired, CancellationToken cancellationToken)
    {
        session.Clear(expired);
        await sessionStore.ClearAsync(cancellationToken);
    }
}
