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
    /// Gets an optional description providing additional details about the health check result.
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
}