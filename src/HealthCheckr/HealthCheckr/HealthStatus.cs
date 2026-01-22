namespace HealthCheckr;

/// <summary>
/// Represents the possible outcomes of a health check execution.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The health state could not be determined.
    /// </summary>
    /// <remarks>
    /// This value is returned when no health checks were executed.
    /// </remarks>
    Unknown = -1,

    /// <summary>
    /// The system is unhealthy and requires immediate attention.
    /// </summary>
    /// <remarks>
    /// Indicates a failure in one or more critical dependencies.
    /// </remarks>
    Unhealthy = 0,

    /// <summary>
    /// The system is operational but experiencing reduced functionality.
    /// </summary>
    /// <remarks>
    /// Typically returned when non-critical checks fail.
    /// </remarks>
    Degraded = 1,

    /// <summary>
    /// The system is fully operational.
    /// </summary>
    Healthy = 2
}