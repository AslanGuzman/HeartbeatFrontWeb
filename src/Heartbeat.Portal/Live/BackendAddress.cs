namespace Heartbeat.Portal.Live;

/// <summary>
/// Raíz del backend a partir de la misma configuración que usa el portal (<c>MissionControlApi:BaseUrl</c>,
/// que apunta a <c>…/api/mc/v1/</c>). Las rutas nuevas (<c>/api/heartrate</c>, <c>/hubs/heartrate</c>,
/// <c>/api/salud</c>) cuelgan de la raíz, no de <c>/api/mc/v1</c>.
/// </summary>
public static class BackendAddress
{
    public static Uri Root(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!Uri.TryCreate(configuration["MissionControlApi:BaseUrl"], UriKind.Absolute, out var missionControl))
        {
            throw new InvalidOperationException("Configure MissionControlApi:BaseUrl with the absolute /api/mc/v1/ URL.");
        }

        return new Uri(missionControl, "/");
    }
}
