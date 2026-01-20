using System.Text.Json.Serialization;

namespace HealthCheckr.Responses;

public sealed class HealthCheckResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public HealthStatus Status { get; internal set; }

    [JsonPropertyName("error")]
    public string? Error { get; internal set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; internal set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; internal set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; internal set; }
}