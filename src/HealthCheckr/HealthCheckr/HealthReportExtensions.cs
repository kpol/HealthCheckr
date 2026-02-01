using HealthCheckr.Responses;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthCheckr;

/// <summary>
/// Provides extension methods for serializing <see cref="HealthReport"/> instances to JSON.
/// </summary>
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

    /// <summary>
    /// Serializes the specified <see cref="HealthReport"/> to a JSON string.
    /// </summary>
    /// <param name="healthReport">The health report to serialize.</param>
    /// <returns>A JSON string representation of the health report.</returns>
    public static string ToJson(this HealthReport healthReport) =>
        JsonSerializer.Serialize(healthReport, Options);

    /// <summary>
    /// Serializes the specified <see cref="HealthReport"/> to JSON and writes it to the provided stream.
    /// </summary>
    /// <param name="healthReport">The health report to serialize.</param>
    /// <param name="stream">The stream to which the JSON will be written.</param>
    public static void ToJson(this HealthReport healthReport, Stream stream) =>
        JsonSerializer.Serialize(stream, healthReport, Options);
}