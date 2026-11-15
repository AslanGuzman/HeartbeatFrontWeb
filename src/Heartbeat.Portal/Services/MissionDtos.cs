using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

// Wire DTOs for the mission/risk endpoints added after the pinned Shared submodule revision.
public sealed record MissionProfileResponse(
    Guid Id,
    string Name,
    string Environment,
    EnvironmentProfileResponse EnvironmentProfile,
    double CommunicationDelaySeconds,
    IsolationIndicesResponse? Indices = null);

public sealed record EnvironmentProfileResponse(
    string Type,
    decimal GravityG,
    decimal DistanceKm,
    int CrewSize,
    decimal HabitableVolumeM3);

public sealed record IsolationIndicesResponse(
    IsolationIndexResponse Isolation,
    IsolationIndexResponse Confinement,
    HealthStatus Status,
    decimal HoursWithoutContact,
    decimal ContinuousStayHours,
    bool Simulated = true);

public sealed record IsolationIndexResponse(
    string IndicatorId,
    string Name,
    decimal Value,
    HealthStatus Status,
    string Label,
    string CatalogStatus,
    SourceLink Source);

public sealed record RiskStatusResponse(
    HealthStatus Status,
    string Explanation,
    IReadOnlyList<HazardStatusResponse> Hazards);

public sealed record HazardStatusResponse(
    Hazard Hazard,
    HealthStatus Status,
    string Explanation,
    decimal? Value = null,
    decimal? Limit = null,
    string? Unit = null,
    SourceLink? Source = null);
