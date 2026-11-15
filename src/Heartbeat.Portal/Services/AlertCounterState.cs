using System.Text.Json;

namespace Heartbeat.Portal.Services;

public sealed class AlertCounterState(MissionControlClient client)
{
    public int? OpenCount { get; private set; }
    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await client.GetAlertsAsync(
                "open",
                page: 1,
                pageSize: 1,
                cancellationToken: cancellationToken);
            OpenCount = alerts.Total;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
                JsonException or
                InvalidDataException or
                TaskCanceledException)
        {
            OpenCount = null;
        }
        finally
        {
            Changed?.Invoke();
        }
    }
}
