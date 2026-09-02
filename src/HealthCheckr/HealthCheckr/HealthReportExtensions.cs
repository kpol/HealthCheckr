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
    /// Defines extensions for the <see cref="HealthReport"/>.
    /// </summary>
    /// <param name="healthReport">The health report to serialize.</param>
    extension(HealthReport healthReport)
    {
        /// <summary>
        /// Serializes the specified <see cref="HealthReport"/> to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of the health report.</returns>
        public string ToJson() => 
            JsonSerializer.Serialize(healthReport, Options);

        /// <summary>
        /// Serializes the specified <see cref="HealthReport"/> to JSON and writes it to the provided stream.
        /// </summary>
        /// <param name="stream">The stream to which the JSON will be written.</param>
        public void ToJson(Stream stream) =>
            JsonSerializer.Serialize(stream, healthReport, Options);

        /// <summary>
        /// Serializes the specified <see cref="HealthReport"/> to JSON asynchronously and writes it to the provided stream.
        /// </summary>
        /// <param name="stream">The stream to which the JSON will be written.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task ToJsonAsync(Stream stream, CancellationToken cancellationToken = default) =>
            JsonSerializer.SerializeAsync(stream, healthReport, Options, cancellationToken);
    }
}