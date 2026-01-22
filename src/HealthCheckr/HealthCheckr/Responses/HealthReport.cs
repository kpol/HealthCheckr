using System.Text.Json.Serialization;

namespace HealthCheckr.Responses;

/// <summary>
/// Represents the aggregated health report produced by the health checking system.
/// Contains the overall status, individual check entries, timing information and
/// optional additional data.
/// </summary>
public struct HealthReport()
{
    /// <summary>
    /// Gets the overall health status computed from individual checks.
    /// </summary>
    [JsonPropertyName("status")]
    public HealthStatus Status { get; internal set; }

    /// <summary>
    /// Gets the list of individual health check results that contributed to the report.
    /// </summary>
    [JsonPropertyName("checks")]
    public IReadOnlyList<HealthReportEntry> Checks { get; internal set; } = [];

    /// <summary>
    /// Gets the total duration of the health check operation in milliseconds.
    /// </summary>
    [JsonPropertyName("totalDurationMs")]
    public long? TotalDurationMs { get; internal set; }

    /// <summary>
    /// Gets the timestamp when the <see cref="HealthReport"/> instance was created.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public readonly DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the optional additional data associated with the health report.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object?>? Data { get; internal set; }

    /// <summary>
    /// Gets the HTTP status code that should be returned to callers for this report.
    /// </summary>
    [JsonIgnore]
    public int HttpStatusCode { get; internal set; }
}