namespace Heartbeat.Portal.Services;

public sealed record PhantomComparisonResponse(
    string Environment,
    string EnvironmentName,
    decimal GravityG,
    int Weeks,
    decimal ExerciseHoursPerWeek,
    string Status,
    SourceLinkResponse Source,
    IReadOnlyList<PhantomSeriesResponse> Series,
    string Note,
    bool Simulated = true);

public sealed record PhantomSeriesResponse(
    PhantomProfileResponse Phantom,
    IReadOnlyList<PhantomPointResponse> Points,
    PhantomPointResponse Final);

public sealed record PhantomProfileResponse(
    Guid Id,
    string Name,
    string Sex,
    int AgeYears,
    decimal MassKg);

public sealed record PhantomPointResponse(
    int Week,
    decimal BoneChangePercent,
    decimal MuscleChangePercent,
    decimal FluidChangePercent,
    decimal IopChangeMmHg);

public sealed record SourceLinkResponse(string Title, Uri Url);
