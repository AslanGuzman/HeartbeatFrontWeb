using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Portal.Services;

namespace Heartbeat.Portal.Salud;

/// <summary>Resultado de una llamada a <c>/api/salud</c>: los datos o un mensaje para mostrar en la página.</summary>
public sealed record SaludResult<T>(T? Data, string? Error);

/// <summary>
/// Llamadas a <c>/api/salud</c> (datos de Samsung Health que el backend lee del backend Python) reutilizando
/// <see cref="MissionControlClient.SendAsync"/>, que añade y renueva el token de la sesión de Control de Misión.
/// </summary>
public static class SaludApi
{
    public const string Unavailable = "El servidor de datos de Samsung Health no responde";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<SaludResult<T>> GetSaludAsync<T>(
        this MissionControlClient client, string pathAndQuery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        try
        {
            // La ruta empieza por "/": se resuelve contra la raíz del backend, no contra /api/mc/v1/.
            using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new SaludResult<T>(await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken), null);
            }

            return response.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable => new SaludResult<T>(default, Unavailable),
                HttpStatusCode.Unauthorized => new SaludResult<T>(default, "La sesión ha caducado. Vuelve a iniciar sesión."),
                _ => new SaludResult<T>(default, $"Error {(int)response.StatusCode}: {await ReadErrorAsync(response, cancellationToken)}"),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new SaludResult<T>(default, $"No se pudo conectar con el backend ({ex.Message})");
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("error", out var error) ? error.GetString() ?? "" : response.ReasonPhrase ?? "";
        }
        catch (JsonException)
        {
            return response.ReasonPhrase ?? "";
        }
    }
}
