using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

public sealed class MissionControlSession
{
    private IReadOnlySet<string> _permissions = new HashSet<string>(StringComparer.Ordinal);

    public PortalUserDto? User { get; private set; }
    public string? AccessToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; private set; }
    public IReadOnlySet<string> Permissions => _permissions;
    public bool SessionExpired { get; private set; }

    public event Action? Changed;

    public bool HasPermission(string permission) =>
        !string.IsNullOrWhiteSpace(permission) && _permissions.Contains(permission);

    public void SetSession(
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt,
        IEnumerable<string> permissions)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
        SessionExpired = false;
        _permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
        Changed?.Invoke();
    }

    public void SetTokens(
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
        SessionExpired = false;
        Changed?.Invoke();
    }

    public void SetUser(PortalUserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        User = user;
        _permissions = new HashSet<string>(user.Permissions, StringComparer.Ordinal);
        Changed?.Invoke();
    }

    internal void UpdateTokens(
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshToken,
        DateTimeOffset? refreshTokenExpiresAt)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
        Changed?.Invoke();
    }

    public void Clear(bool expired = false)
    {
        User = null;
        AccessToken = null;
        AccessTokenExpiresAt = null;
        RefreshToken = null;
        RefreshTokenExpiresAt = null;
        _permissions = new HashSet<string>(StringComparer.Ordinal);
        SessionExpired = expired;
        Changed?.Invoke();
    }
}
