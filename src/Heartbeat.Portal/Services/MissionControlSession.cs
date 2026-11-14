namespace Heartbeat.Portal.Services;

public sealed class MissionControlSession
{
    private IReadOnlySet<string> _permissions = new HashSet<string>(StringComparer.Ordinal);

    public string? AccessToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; private set; }
    public IReadOnlySet<string> Permissions => _permissions;

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
        _permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
        Changed?.Invoke();
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        _permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
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

    public void Clear()
    {
        AccessToken = null;
        AccessTokenExpiresAt = null;
        RefreshToken = null;
        RefreshTokenExpiresAt = null;
        _permissions = new HashSet<string>(StringComparer.Ordinal);
        Changed?.Invoke();
    }
}
