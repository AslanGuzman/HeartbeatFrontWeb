using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

namespace Heartbeat.Portal.Services;

// Browser storage only receives Data Protection ciphertext; JWTs stay server-side.
public sealed class MissionControlSessionStore(
    IJSRuntime jsRuntime,
    IDataProtectionProvider protectionProvider,
    MissionControlSession session) : IMissionControlSessionStore
{
    private const string StorageKey = "heartbeat.mc.session";
    private readonly IDataProtector _protector =
        protectionProvider.CreateProtector("Heartbeat.Portal.MissionControlSession.v1");
    private IJSObjectReference? _module;
    private bool _restored;

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_restored)
        {
            return;
        }

        var protectedState = await (await GetModuleAsync(cancellationToken))
            .InvokeAsync<string?>("read", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(protectedState))
        {
            _restored = true;
            return;
        }

        try
        {
            var json = _protector.Unprotect(protectedState);
            var tokens = JsonSerializer.Deserialize<PersistedTokens>(json);
            if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                throw new JsonException("The stored session is incomplete.");
            }

            session.SetTokens(
                tokens.AccessToken,
                tokens.AccessTokenExpiresAt,
                tokens.RefreshToken,
                tokens.RefreshTokenExpiresAt);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            await ClearAsync(cancellationToken);
        }

        _restored = true;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken) ||
            session.AccessTokenExpiresAt is null ||
            session.RefreshTokenExpiresAt is null)
        {
            await ClearAsync(cancellationToken);
            return;
        }

        var tokens = new PersistedTokens(
            session.AccessToken,
            session.AccessTokenExpiresAt.Value,
            session.RefreshToken,
            session.RefreshTokenExpiresAt.Value);
        var json = JsonSerializer.Serialize(tokens);
        var protectedState = _protector.Protect(json);

        await (await GetModuleAsync(cancellationToken))
            .InvokeVoidAsync("write", cancellationToken, StorageKey, protectedState);
        _restored = true;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await (await GetModuleAsync(cancellationToken))
            .InvokeVoidAsync("remove", cancellationToken, StorageKey);
        _restored = true;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            "./session-store.js");
        return _module;
    }

    private sealed record PersistedTokens(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt);
}
