using System.Text.Json.Serialization;

namespace Heartbeat.Portal.Salud;

// Copia de Heartbeat.Api/Salud/WatchSyncDtos.cs: mismas clases y nombres JSON que devuelve /api/salud del backend.
// Los campos opcionales son anulables: el backend Python a veces los omite en lugar de enviarlos como null.

public sealed class LatestValueDto
{
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("value2")] public double? Value2 { get; set; }          // diastólica en blood_pressure
    [JsonPropertyName("unit")] public string? Unit { get; set; }
    [JsonPropertyName("measured_at")] public DateTimeOffset MeasuredAt { get; set; }
}

public sealed class PeriodStatsDto
{
    [JsonPropertyName("from")] public DateOnly From { get; set; }
    [JsonPropertyName("to")] public DateOnly To { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("avg")] public double? Avg { get; set; }
    [JsonPropertyName("min")] public double? Min { get; set; }
    [JsonPropertyName("max")] public double? Max { get; set; }
}

public sealed class StageMinutesDto
{
    [JsonPropertyName("awake")] public int Awake { get; set; }
    [JsonPropertyName("light")] public int Light { get; set; }
    [JsonPropertyName("deep")] public int Deep { get; set; }
    [JsonPropertyName("rem")] public int Rem { get; set; }
}

public sealed class SleepRecordDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("start_at")] public DateTimeOffset StartAt { get; set; }
    [JsonPropertyName("end_at")] public DateTimeOffset EndAt { get; set; }
    [JsonPropertyName("tz_offset_min")] public int TzOffsetMin { get; set; }
    [JsonPropertyName("duration_min")] public int DurationMin { get; set; }
    [JsonPropertyName("sleep_score")] public int? SleepScore { get; set; }    // casi siempre null en este reloj
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("stage_minutes")] public StageMinutesDto? StageMinutes { get; set; } // falta si la noche no tiene fases
}

public sealed class DailyActivityDto
{
    [JsonPropertyName("local_date")] public DateOnly LocalDate { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("steps")] public int? Steps { get; set; }
    [JsonPropertyName("distance_m")] public double? DistanceM { get; set; }
    [JsonPropertyName("active_calories_kcal")] public double? ActiveCaloriesKcal { get; set; }
    [JsonPropertyName("total_calories_kcal")] public double? TotalCaloriesKcal { get; set; }
    [JsonPropertyName("active_minutes")] public int? ActiveMinutes { get; set; }
    [JsonPropertyName("floors_climbed")] public double? FloorsClimbed { get; set; }
    [JsonPropertyName("step_goal")] public int? StepGoal { get; set; }
}

public sealed class LatestValuesDto
{
    [JsonPropertyName("heart_rate")] public LatestValueDto? HeartRate { get; set; }
    [JsonPropertyName("spo2")] public LatestValueDto? Spo2 { get; set; }
    [JsonPropertyName("blood_pressure")] public LatestValueDto? BloodPressure { get; set; } // Value = sistólica, Value2 = diastólica
    [JsonPropertyName("skin_temperature")] public LatestValueDto? SkinTemperature { get; set; }
    [JsonPropertyName("weight_kg")] public LatestValueDto? WeightKg { get; set; }
    [JsonPropertyName("body_fat_pct")] public LatestValueDto? BodyFatPct { get; set; }
    [JsonPropertyName("energy_score")] public LatestValueDto? EnergyScore { get; set; }
}

public sealed class DailySummaryDto
{
    [JsonPropertyName("date")] public DateOnly Date { get; set; }
    [JsonPropertyName("latest")] public LatestValuesDto Latest { get; set; } = new();
    [JsonPropertyName("heart_rate_day")] public PeriodStatsDto? HeartRateDay { get; set; }
    [JsonPropertyName("sleep")] public SleepRecordDto? Sleep { get; set; }
    [JsonPropertyName("activity")] public DailyActivityDto? Activity { get; set; }
    [JsonPropertyName("unacknowledged_alerts")] public int UnacknowledgedAlerts { get; set; }
}

public sealed class PointDto
{
    [JsonPropertyName("t")] public DateTimeOffset T { get; set; }              // inicio del intervalo (o instante si raw)
    [JsonPropertyName("avg")] public double Avg { get; set; }
    [JsonPropertyName("min")] public double? Min { get; set; }
    [JsonPropertyName("max")] public double? Max { get; set; }
    [JsonPropertyName("count")] public int? Count { get; set; }
}

public sealed class SeriesDto
{
    [JsonPropertyName("metric")] public string Metric { get; set; } = "";
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";          // "lpm"
    [JsonPropertyName("resolution")] public string Resolution { get; set; } = "";
    [JsonPropertyName("points")] public List<PointDto> Points { get; set; } = new();
    [JsonPropertyName("stats")] public PeriodStatsDto? Stats { get; set; }
}

public sealed class BloodPressureDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("measured_at")] public DateTimeOffset MeasuredAt { get; set; }
    [JsonPropertyName("tz_offset_min")] public int TzOffsetMin { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("systolic")] public int Systolic { get; set; }
    [JsonPropertyName("diastolic")] public int Diastolic { get; set; }
    [JsonPropertyName("mean_pressure")] public int? MeanPressure { get; set; }
    [JsonPropertyName("pulse_bpm")] public int? PulseBpm { get; set; }
    [JsonPropertyName("medication_taken")] public bool? MedicationTaken { get; set; }
}

public sealed class SyncStatusDto
{
    [JsonPropertyName("data_type")] public string DataType { get; set; } = ""; // heart_rate, spo2, skin_temperature, blood_pressure, body_composition, sleep, steps_hourly, daily_activity, exercise, energy_score, health_event
    [JsonPropertyName("last_synced_at")] public DateTimeOffset LastSyncedAt { get; set; }
    [JsonPropertyName("last_measured_at")] public DateTimeOffset? LastMeasuredAt { get; set; }
}
