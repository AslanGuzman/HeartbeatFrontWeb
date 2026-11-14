namespace Heartbeat.Portal.Services;

public interface IMissionControlSessionStore
{
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
