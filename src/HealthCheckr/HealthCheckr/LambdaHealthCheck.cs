namespace HealthCheckr;

internal sealed class LambdaHealthCheck(Func<CancellationToken, Task<HealthCheckResult>> Check) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default) => Check(cancellationToken);
}