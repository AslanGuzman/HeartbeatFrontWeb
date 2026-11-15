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
    IReadOnlyList<HazardStatusResponse> Hazards,
    string? ProfileId = null);

public sealed record HazardStatusResponse(
    Hazard Hazard,
    HealthStatus Status,
    string Explanation,
    decimal? Value = null,
    decimal? Limit = null,
    string? Unit = null,
    SourceLink? Source = null);

// Domain profiles (CONTRACTS.md §10, GET /api/mc/v1/profiles). Thresholds come from the server catalog.
public sealed record DomainProfileResponse(
    string Id,
    string Name,
    string Description,
    EnvironmentProfileResponse Environment,
    string Status,
    SourceLink Source,
    IReadOnlyList<ProfileHazardResponse> Hazards,
    bool Simulated = true);

public sealed record ProfileHazardResponse(
    Hazard Hazard,
    IReadOnlyList<ProfileIndicatorResponse> Indicators);

public sealed record ProfileIndicatorResponse(
    string IndicatorId,
    string Name,
    string Unit,
    string CatalogStatus,
    SourceLink Source,
    bool Overridden,
    IReadOnlyList<ProfileBandResponse> Bands);

// Min <= value < Max
public sealed record ProfileBandResponse(HealthStatus Status, decimal? Min, decimal? Max);
