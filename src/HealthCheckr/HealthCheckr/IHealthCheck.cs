namespace HealthCheckr;

/// <summary>
/// Represents a health check that can be executed asynchronously.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Executes the health check and returns a <see cref="HealthCheckResult"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while performing the check.
    /// This token may be triggered by a timeout configured in <see cref="HealthChecker"/>
    /// or by external cancellation requested by the caller.
    /// </param>
    /// <returns>A <see cref="Task{HealthCheckResult}"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Implementations should respect the <paramref name="cancellationToken"/> to support cooperative cancellation.
    /// If the token is cancelled, an <see cref="OperationCanceledException"/> should be thrown.
    /// </remarks>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}