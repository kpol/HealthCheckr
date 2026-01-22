using HealthCheckr.Responses;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthCheckr;

public static class HealthReportExtensions
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

    public static string ToJson(this HealthReport healthReport) => JsonSerializer.Serialize(healthReport, Options);

    public static void ToJson(this HealthReport healthReport, Stream stream) => JsonSerializer.Serialize(stream, healthReport, Options);
}
