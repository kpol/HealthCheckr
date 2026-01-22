using System.Text.Json.Serialization;

namespace HealthCheckr.Responses;

/// <summary>
/// Represents a single entry in a health report produced by health checks.
/// </summary>
public struct HealthReportEntry()
{
    /// <summary>
    /// The logical name of the health check.
    /// </summary>
    /// <value>A non-null, required string identifying the check.</value>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// A short, human-readable description of what the check verifies.
    /// </summary>
    /// <value>An optional string providing additional context; may be <c>null</c>.</value>
    [JsonPropertyName("description")]
    public string? Description { get; internal set; }

    /// <summary>
    /// The resulting health status for this check.
    /// </summary>
    /// <value>A <see cref="HealthStatus"/> value indicating the current status.</value>
    [JsonPropertyName("status")]
    public HealthStatus Status { get; internal set; }

    /// <summary>
    /// An error message or exception detail associated with a failing check.
    /// </summary>
    /// <value>An optional diagnostic string; may be <c>null</c> when successful.</value>
    [JsonPropertyName("error")]
    public string? Error { get; internal set; }

    /// <summary>
    /// The elapsed time for the check execution in milliseconds.
    /// </summary>
    /// <value>A nullable long representing duration in milliseconds; may be <c>null</c>.</value>
    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; internal set; }

    /// <summary>
    /// Arbitrary key/value data produced by the check.
    /// </summary>
    /// <value>A read-only dictionary of additional diagnostic or metadata values; may be <c>null</c>.</value>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object?>? Data { get; internal set; }

    /// <summary>
    /// Tags associated with the health check, useful for grouping or filtering.
    /// </summary>
    /// <value>An optional array of string tags; may be <c>null</c> or empty.</value>
    [JsonPropertyName("tags")]
    public string[]? Tags { get; internal set; }
}