using HealthCheckr.Responses;
using System.Diagnostics;

namespace HealthCheckr;

public sealed class HealthChecker
{
    private readonly List<(string Name, string? Description, Func<CancellationToken, Task<HealthStatus>> Check)> _checks = [];

    public int HealthyHttpStatusCode { get; init; } = 200;

    public int DegradedHttpStatusCode { get; init; } = 200;

    public int UnhealthyHttpStatusCode { get; init; } = 503;

    public bool IncludeErrors { get; init; } = false;

    public bool IncludeDuration { get; init; } = true;

    public HealthChecker AddCheck(string name, Func<CancellationToken, Task<HealthStatus>> check, string? description = null)
    {
        _checks.Add((name, description, check));

        return this;
    }

    public HealthChecker AddCheck(string name, Func<Task<HealthStatus>> check, string? description = null) => AddCheck(name, _ => check(), description);

    public async Task<HealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = IncludeDuration ? Stopwatch.StartNew() : null;

        HealthResult healthResponse = new();

        var tasks = _checks.Select(async (check, index) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var start = IncludeDuration ? stopwatch!.ElapsedMilliseconds : 0;

            var healthCheckEntry = await ExecuteCheckAsync(check.Name, check.Description, check.Check, cancellationToken);

            if (IncludeDuration)
            {
                healthCheckEntry.DurationMs = stopwatch!.ElapsedMilliseconds - start;
            }

            return (Index: index, HealthCheckEntry: healthCheckEntry);
        });

        var result = await Task.WhenAll(tasks);

        healthResponse.Checks.AddRange(result.OrderBy(r => r.Index).Select(r => r.HealthCheckEntry));

        stopwatch?.Stop();

        if (IncludeDuration)
        {
            healthResponse.TotalDurationMs = stopwatch!.ElapsedMilliseconds;
        }

        healthResponse.Status = GetOverallStatus(healthResponse.Checks);
        healthResponse.HttpStatusCode = GetHttpStatusCode(healthResponse.Status);

        return healthResponse;
    }

    private static HealthStatus GetOverallStatus(IEnumerable<HealthCheckResult> checks)
    {
        HealthStatus overallStatus = HealthStatus.Healthy;

        foreach (var check in checks)
        {
            if (check.Status == HealthStatus.Unhealthy)
            {
                return HealthStatus.Unhealthy;
            }

            if (check.Status == HealthStatus.Degraded)
            {
                overallStatus = HealthStatus.Degraded;
            }
        }

        return overallStatus;
    }

    private async Task<HealthCheckResult> ExecuteCheckAsync(string name, string? description, Func<CancellationToken, Task<HealthStatus>> check, CancellationToken cancellationToken)
    {
        HealthCheckResult healthCheckEntry = new() { Name = name, Description = description };

        try
        {
            var result = await check(cancellationToken);
            healthCheckEntry.Status = result;
        }
        catch (Exception ex)
        {
            healthCheckEntry.Status = HealthStatus.Unhealthy;

            if (IncludeErrors)
            {
                healthCheckEntry.Error = ex.ToString();
            }
        }

        return healthCheckEntry;
    }

    private int GetHttpStatusCode(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => HealthyHttpStatusCode,
        HealthStatus.Degraded => DegradedHttpStatusCode,
        HealthStatus.Unhealthy => UnhealthyHttpStatusCode,
        _ => UnhealthyHttpStatusCode
    };
}