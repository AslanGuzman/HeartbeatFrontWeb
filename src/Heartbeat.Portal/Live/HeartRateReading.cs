using System.Text.Json.Serialization;

namespace Heartbeat.Portal.Live;

/// <summary>Lectura de frecuencia cardíaca del Galaxy Watch 5, tal como la difunde el backend (copia de Heartbeat.Api/Live).</summary>
public sealed record HeartRateReading(
    [property: JsonPropertyName("bpm")] int Bpm,
    [property: JsonPropertyName("measuredAt")] DateTimeOffset MeasuredAt,
    [property: JsonPropertyName("source")] string? Source);
