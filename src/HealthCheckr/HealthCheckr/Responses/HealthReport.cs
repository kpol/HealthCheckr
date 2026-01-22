using System.Text.Json.Serialization;

namespace HealthCheckr.Responses;

public struct HealthReport
{
    public HealthReport() { }

    [JsonPropertyName("status")]
    public HealthStatus Status { get; internal set; }

    [JsonPropertyName("checks")]
    public IReadOnlyList<HealthReportEntry> Checks { get; internal set; } = [];

    [JsonPropertyName("totalDurationMs")]
    public long? TotalDurationMs { get; internal set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object?>? Data { get; internal set; }

    [JsonIgnore]
    public int HttpStatusCode { get; internal set; }
}