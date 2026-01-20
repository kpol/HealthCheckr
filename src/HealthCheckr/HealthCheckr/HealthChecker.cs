using HealthCheckr.Responses;
using System.Diagnostics;

namespace HealthCheckr;

public sealed class HealthChecker
{
    private readonly List<HealthCheckRegistration> _checks = [];

    public int HealthyHttpStatusCode { get; init; } = 200;

    public int DegradedHttpStatusCode { get; init; } = 200;

    public int UnhealthyHttpStatusCode { get; init; } = 503;

    public bool IncludeErrors { get; init; } = false;

    public bool IncludeDuration { get; init; } = true;

    public Dictionary<string, object?>? Metadata { get; init; } = [];

    public HealthChecker AddCheck(string name, Func<CancellationToken, Task<HealthStatus>> check, string? description = null, Dictionary<string, object?>? metadata = null, IEnumerable<string>? tags = null)
    {
        var tagArray = tags?.ToArray();

        _checks.Add(new(
            name,
            description,
            metadata?.Count > 0 ? new Dictionary<string, object?>(metadata) : null,
            tagArray?.Length > 0 ? tagArray : null,
            check));

        return this;
    }

    public HealthChecker AddCheck(
        string name,
        Func<Task<HealthStatus>> check,
        string? description = null,
        Dictionary<string, object?>? metadata = null,
        IEnumerable<string>? tags = null)
        => AddCheck(name, _ => check(), description, metadata, tags);

    public async Task<HealthResult> CheckAsync(IEnumerable<string>? includeTags = null, IEnumerable<string>? excludeTags = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = IncludeDuration ? Stopwatch.StartNew() : null;

        HealthResult healthResponse = new();

        if (Metadata?.Count > 0)
        {
            healthResponse.Metadata = new Dictionary<string, object?>(Metadata);
        }

        HashSet<string>? includeTagsSet = includeTags is not null && includeTags.Any() ? [.. includeTags] : null;
        HashSet<string>? excludeTagsSet = excludeTags is not null && excludeTags.Any() ? [.. excludeTags] : null;

        var tasks = _checks
            .Where(c => ShouldRun(c.Tags, includeTagsSet, excludeTagsSet))
            .Select(async (check, index) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var start = IncludeDuration ? stopwatch!.ElapsedMilliseconds : 0;

                var healthCheckEntry = await ExecuteCheckAsync(check.Name, check.Description, check.Check, cancellationToken);

                if (IncludeDuration)
                {
                    healthCheckEntry.DurationMs = stopwatch!.ElapsedMilliseconds - start;
                }

                healthCheckEntry.Metadata = check.Metadata;
                healthCheckEntry.Tags = check.Tags;

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

    private static bool ShouldRun(
        string[]? checkTags,
        HashSet<string>? include,
        HashSet<string>? exclude)
    {
        if (checkTags is null || checkTags.Length == 0)
        {
            return include is null && exclude is null;
        }

        if (exclude is not null && checkTags.Any(exclude.Contains))
        {
            return false;
        }

        if (include is not null)
        {
            return checkTags.Any(include.Contains);
        }

        return true;
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

    private int GetHttpStatusCode(HealthStatus status)
        => status switch
        {
            HealthStatus.Healthy => HealthyHttpStatusCode,
            HealthStatus.Degraded => DegradedHttpStatusCode,
            HealthStatus.Unhealthy => UnhealthyHttpStatusCode,
            _ => UnhealthyHttpStatusCode
        };

    private sealed record HealthCheckRegistration(
        string Name,
        string? Description,
        Dictionary<string, object?>? Metadata,
        string[]? Tags,
        Func<CancellationToken, Task<HealthStatus>> Check);
}