using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthCheckr.Responses;

public sealed class HealthResult
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    [JsonPropertyName("status")]
    public HealthStatus Status { get; internal set; }

    [JsonPropertyName("checks")]
    public List<HealthCheckResult> Checks { get; } = [];

    [JsonPropertyName("totalDurationMs")]
    public long? TotalDurationMs { get; internal set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; internal set; }

    [JsonIgnore]
    public int HttpStatusCode { get; internal set; }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}