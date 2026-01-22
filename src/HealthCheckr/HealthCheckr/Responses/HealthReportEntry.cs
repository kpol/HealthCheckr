using System.Text.Json.Serialization;

namespace HealthCheckr.Responses;

public struct HealthReportEntry
{
    public HealthReportEntry() { }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; internal set; }

    [JsonPropertyName("status")]
    public HealthStatus Status { get; internal set; }

    [JsonPropertyName("error")]
    public string? Error { get; internal set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; internal set; }

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object?>? Data { get; internal set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; internal set; }
}
