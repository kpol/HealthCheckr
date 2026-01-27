namespace HealthCheckr;

/// <summary>
/// Represents the result of a single health check.
/// </summary>
public readonly struct HealthCheckResult
{
    /// <summary>
    /// Gets the overall status of the health check.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// A human-readable description of the status of the component that was checked.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the exception that caused the health check to fail, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets optional additional data associated with the health check result.
    /// This can include metrics, dependency information, or other diagnostic data.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Data { get; init; }

    /// <summary>
    /// Creates a <see cref="HealthCheckResult"/> representing a healthy component.
    /// </summary>
    /// <param name="description">A human-readable description of the status of the component that was checked. Optional.</param>
    /// <param name="data">Additional key-value pairs describing the health of the component. Optional.</param>
    /// <returns>A <see cref="HealthCheckResult"/> representing a healthy component.</returns>
    public static HealthCheckResult Healthy(string? description = null, IReadOnlyDictionary<string, object?>? data = null) =>
        new() { Status = HealthStatus.Healthy, Description = description, Data = data };

    /// <summary>
    /// Creates a <see cref="HealthCheckResult"/> representing a degraded component.
    /// </summary>
    /// <param name="description">A human-readable description of the status of the component that was checked. Optional.</param>
    /// <param name="exception">An <see cref="Exception"/> representing the exception that was thrown when checking for status. Optional.</param>
    /// <param name="data">Additional key-value pairs describing the health of the component. Optional.</param>
    /// <returns>A <see cref="HealthCheckResult"/> representing a healthy component.</returns>
    public static HealthCheckResult Degraded(string? description = null, Exception? exception = null, IReadOnlyDictionary<string, object?>? data = null) =>
        new() { Status = HealthStatus.Degraded, Description = description, Exception = exception, Data = data };

    /// <summary>
    /// Creates a <see cref="HealthCheckResult"/> representing a unhealthy component.
    /// </summary>
    /// <param name="description">A human-readable description of the status of the component that was checked. Optional.</param>
    /// <param name="exception">An <see cref="Exception"/> representing the exception that was thrown when checking for status. Optional.</param>
    /// <param name="data">Additional key-value pairs describing the health of the component. Optional.</param>
    /// <returns>A <see cref="HealthCheckResult"/> representing a healthy component.</returns>
    public static HealthCheckResult Unhealthy(string? description = null, Exception? exception = null, IReadOnlyDictionary<string, object?>? data = null) =>
        new() { Status = HealthStatus.Unhealthy, Description = description, Exception = exception, Data = data };
}