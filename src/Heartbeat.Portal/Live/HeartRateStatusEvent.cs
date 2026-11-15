using Heartbeat.Shared;

namespace Heartbeat.Portal.Live;

public sealed record HeartRateStatusEvent(
    int Bpm,
    DateTimeOffset MeasuredAt,
    HealthStatus Status,
    IReadOnlyList<string> BandIds);
