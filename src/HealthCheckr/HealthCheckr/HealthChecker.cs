using HealthCheckr.Responses;
using System.Diagnostics;

namespace HealthCheckr;

/// <summary>
/// Executes registered health checks and produces either a detailed JSON-style report
/// or a lightweight overall health status.
/// </summary>
/// <remarks>
/// - Full checks are executed in parallel and return per-check details.
/// - Simple checks are executed concurrently and short-circuit as soon as any check reports failure.
/// </remarks>
public sealed class HealthChecker
{
    private readonly Dictionary<string, HealthCheckRegistration> _checks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// HTTP status code returned when the overall health status is <see cref="HealthStatus.Healthy"/>.
    /// </summary>
    public int HealthyHttpStatusCode { get; init; } = 200;

    /// <summary>
    /// HTTP status code returned when the overall health status is <see cref="HealthStatus.Degraded"/>.
    /// </summary>
    public int DegradedHttpStatusCode { get; init; } = 200;

    /// <summary>
    /// HTTP status code returned when the overall health status is <see cref="HealthStatus.Unhealthy"/>.
    /// </summary>
    public int UnhealthyHttpStatusCode { get; init; } = 503;

    /// <summary>
    /// Indicates whether error messages should be included in the health report.
    /// </summary>
    public bool IncludeErrors { get; init; } = true;

    /// <summary>
    /// Indicates whether full stack traces should be included when errors are reported.
    /// </summary>
    public bool IncludeStackTrace { get; init; } = false;

    /// <summary>
    /// Indicates whether execution duration should be measured and included.
    /// </summary>
    public bool IncludeDuration { get; init; } = true;

    /// <summary>
    /// Optional global data attached to the health report.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Data { get; init; }

    /// <summary>
    /// Registers a health check with an asynchronous execution delegate.
    /// </summary>
    /// <param name="name">A unique name used to identify the health check.</param>
    /// <param name="check">Delegate that executes the check.</param>
    /// <param name="tags">Optional tags used for filtering.</param>
    /// <param name="timeout">
    /// Optional timeout for the health check execution.
    /// When it elapses the check is reported as <see cref="HealthStatus.Unhealthy"/>,
    /// even if the <paramref name="check"/> delegate ignores cancellation.
    /// The delegate should still observe the token – for example by passing it to I/O calls
    /// such as <see cref="HttpClient"/> methods or <see cref="Task.Delay(TimeSpan, CancellationToken)"/> –
    /// so that the underlying work stops instead of continuing in the background.
    /// </param>
    /// <returns>The current <see cref="HealthChecker"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="check"/> is null.</exception>
    public HealthChecker AddCheck(
        string name,
        Func<CancellationToken, Task<HealthCheckResult>> check,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(check);

        return AddCheck(name, new LambdaHealthCheck(check), tags, timeout);
    }

    /// <summary>
    /// Registers a health check without a cancellation token.
    /// </summary>
    /// <remarks>
    /// This overload does not support cooperative cancellation.
    /// Use the CancellationToken overload to enable timeouts.
    /// </remarks>
    /// <param name="name">A unique name used to identify the health check.</param>
    /// <param name="check">Delegate that executes the check.</param>
    /// <param name="tags">Optional tags used for filtering.</param>
    /// <returns>The current <see cref="HealthChecker"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="check"/> is null.</exception>
    public HealthChecker AddCheck(
        string name,
        Func<Task<HealthCheckResult>> check,
        IEnumerable<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(check);

        if (_checks.ContainsKey(name))
            throw new ArgumentException($"A health check with name '{name}' is already registered.", nameof(name));

        return AddCheck(name, _ => check(), tags, null);
    }

    /// <summary>
    /// Registers a health check with the specified name.
    /// </summary>
    /// <param name="name">A unique name used to identify the health check.</param>
    /// <param name="check">The health check implementation to execute.</param>
    /// <param name="tags">Optional tags used for filtering.</param>
    /// <param name="timeout">Optional timeout that limits how long the health check is allowed to run.</param>
    /// <returns>The current <see cref="HealthChecker"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="check"/> is null.</exception>
    public HealthChecker AddCheck(
        string name, 
        IHealthCheck check, 
        IEnumerable<string>? tags = null, 
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(check);

        if (_checks.ContainsKey(name))
            throw new ArgumentException($"A health check with name '{name}' is already registered.", nameof(name));

        // Read-only so the tags exposed through reports and descriptors can't alter filtering
        string[]? tagArray = tags?.Distinct().ToArray();

        _checks.Add(name, new(
            _checks.Count,
            name,
            check,
            tagArray?.Length > 0 ? Array.AsReadOnly(tagArray) : null,
            timeout));

        return this;
    }

    /// <summary>
    /// Executes all matching health checks in parallel and returns a detailed report.
    /// </summary>
    /// <param name="includeTags">Tags that must be present for a check to run.</param>
    /// <param name="excludeTags">Tags that prevent a check from running.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task{HealthReport}"/> representing the asynchronous operation.</returns>
    public async Task<HealthReport> CheckAsync(
        IEnumerable<string>? includeTags = null,
        IEnumerable<string>? excludeTags = null,
        CancellationToken cancellationToken = default)
    {
        var filteredChecks = FilterChecks(includeTags, excludeTags);
        return await CheckInternalAsync(filteredChecks, cancellationToken);
    }

    /// <summary>
    /// Executes all health checks that match the specified predicate in parallel
    /// and returns a detailed health report.
    /// </summary>
    /// <param name="predicate">
    /// A predicate used to select which health checks should be executed.
    /// The predicate receives a <see cref="HealthCheckDescriptor"/> that exposes
    /// metadata such as the check name and tags without allowing execution or mutation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Task{HealthReport}"/> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    public Task<HealthReport> CheckAsync(
        Func<HealthCheckDescriptor, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var checks = _checks.Values
            .Where(c => predicate(new HealthCheckDescriptor(c)));

        return CheckInternalAsync(checks, cancellationToken);
    }

    /// <summary>
    /// Executes a single named health check and returns a detailed <see cref="HealthReport"/>.
    /// </summary>
    /// <param name="checkName">The name of the health check to execute.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task{HealthReport}"/> representing the asynchronous operation. 
    /// The task result contains the detailed health report for the specified check.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="checkName"/> is null or empty.</exception>
    public async Task<HealthReport> CheckAsync(
        string checkName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkName);

        IEnumerable<HealthCheckRegistration> checks = _checks.TryGetValue(checkName, out var registration)
            ? [registration]
            : [];

        return await CheckInternalAsync(checks, cancellationToken);
    }

    /// <summary>
    /// Executes matching health checks concurrently and returns the overall status only.
    /// </summary>
    /// <remarks>
    /// All matching checks are started immediately. As soon as any check reports
    /// <see cref="HealthStatus.Unhealthy"/>, the method returns that result and the
    /// remaining in-flight checks are signalled to cancel cooperatively.
    /// </remarks>
    /// <param name="includeTags">Tags that must be present for a check to run.</param>
    /// <param name="excludeTags">Tags that prevent a check from running.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The overall <see cref="HealthStatus"/> of the executed checks.</returns>
    public async Task<HealthStatus> CheckSimpleAsync(
        IEnumerable<string>? includeTags = null,
        IEnumerable<string>? excludeTags = null,
        CancellationToken cancellationToken = default)
    {
        var checks = FilterChecks(includeTags, excludeTags).OrderBy(c => c.Index);
        return await CheckSimpleAsync([.. checks], cancellationToken);
    }

    /// <summary>
    /// Executes health checks that match the specified predicate concurrently
    /// and returns the overall health status only.
    /// </summary>
    /// <remarks>
    /// All matching checks are started immediately. As soon as any check reports
    /// <see cref="HealthStatus.Unhealthy"/>, the method returns that result and the
    /// remaining in-flight checks are signalled to cancel cooperatively.
    /// </remarks>
    /// <param name="predicate">
    /// A predicate used to select which health checks should be executed.
    /// The predicate receives a <see cref="HealthCheckDescriptor"/> that provides
    /// read-only metadata such as the check name and tags.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The overall <see cref="HealthStatus"/> of the executed checks.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    public Task<HealthStatus> CheckSimpleAsync(
        Func<HealthCheckDescriptor, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var checks = _checks.Values
            .Where(c => predicate(new HealthCheckDescriptor(c)))
            .OrderBy(c => c.Index);

        return CheckSimpleAsync(checks, cancellationToken);
    }

    /// <summary>
    /// Executes a single named health check and returns the overall status.
    /// </summary>
    /// <param name="checkName">The name of the health check to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The overall <see cref="HealthStatus"/> of the specified check.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="checkName"/> is null or empty.</exception>
    public async Task<HealthStatus> CheckSimpleAsync(
        string checkName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkName);

        IEnumerable<HealthCheckRegistration> checks = _checks.TryGetValue(checkName, out var registration)
            ? [registration]
            : [];

        return await CheckSimpleAsync(checks, cancellationToken);
    }

    /// <summary>
    /// Determines whether a health check should run based on include and exclude tag filters.
    /// </summary>
    private static bool ShouldRun(
        IReadOnlyList<string>? checkTags,
        HashSet<string>? include,
        HashSet<string>? exclude)
    {
        // No tags on the check – only run when no filtering is applied
        if (checkTags is not { Count: > 0 })
            return include is null && exclude is null;

        // Exclude always wins
        if (exclude?.Overlaps(checkTags) == true)
            return false;

        // Include acts as a whitelist
        if (include is not null)
            return include.Overlaps(checkTags);

        // No include filter – run by default
        return true;
    }

    /// <summary>
    /// Aggregates individual health statuses into a single overall status.
    /// </summary>
    private static HealthStatus GetOverallStatus(IEnumerable<HealthStatus> statuses)
    {
        HealthStatus overallStatus = HealthStatus.Healthy;

        foreach (var status in statuses)
        {
            if (IsFailure(status))
                return HealthStatus.Unhealthy;

            if (status == HealthStatus.Degraded)
                overallStatus = HealthStatus.Degraded;
        }

        return overallStatus;
    }

    /// <summary>
    /// Determines whether a status reported by a check counts as a failure.
    /// <see cref="HealthStatus.Unknown"/> and undefined values count as failures
    /// because the check did not confirm the component is healthy.
    /// </summary>
    private static bool IsFailure(HealthStatus status)
        => status is not (HealthStatus.Healthy or HealthStatus.Degraded);

    #region Simple Checks

    private static async Task<HealthStatus> CheckSimpleAsync(
        IEnumerable<HealthCheckRegistration> filteredChecks,
        CancellationToken cancellationToken)
    {
        var checks = filteredChecks.ToList();

        if (checks.Count == 0)
            return HealthStatus.Unknown;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var remaining = checks
            .Select(check => RunSingleSimpleCheckAsync(check, linkedCts.Token, cancellationToken))
            .ToList();

        var overall = HealthStatus.Healthy;

        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining);
            remaining.Remove(completed);

            HealthStatus status;

            try
            {
                status = await completed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (IsFailure(status))
            {
                linkedCts.Cancel();
                return HealthStatus.Unhealthy;
            }

            if (status == HealthStatus.Degraded)
                overall = HealthStatus.Degraded;
        }

        return overall;
    }

    private static async Task<HealthStatus> RunSingleSimpleCheckAsync(
        HealthCheckRegistration check,
        CancellationToken linkedToken,
        CancellationToken originalToken)
    {
        CancellationTokenSource? timeoutCts = null;

        try
        {
            var effectiveToken = linkedToken;

            if (check.Timeout is not null)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
                timeoutCts.CancelAfter(check.Timeout.Value);
                effectiveToken = timeoutCts.Token;
            }

            var result = await RunCheckAsync(check.Check, effectiveToken);
            return result.Status;
        }
        catch (OperationCanceledException) when (originalToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    #endregion

    /// <summary>
    /// Filters registered health checks using include and exclude tag sets.
    /// </summary>
    private IEnumerable<HealthCheckRegistration> FilterChecks(
        IEnumerable<string>? includeTags,
        IEnumerable<string>? excludeTags)
    {
        HashSet<string>? include = includeTags is not null && includeTags.Any()
            ? [.. includeTags]
            : null;

        HashSet<string>? exclude = excludeTags is not null && excludeTags.Any()
            ? [.. excludeTags]
            : null;

        return _checks.Values.Where(c => ShouldRun(c.Tags, include, exclude));
    }

    /// <summary>
    /// Executes health checks in parallel and builds a detailed health report.
    /// </summary>
    private async Task<HealthReport> CheckInternalAsync(
        IEnumerable<HealthCheckRegistration> filteredChecks,
        CancellationToken cancellationToken)
    {
        List<HealthCheckRegistration> checks = [.. filteredChecks];

        if (checks.Count == 0)
            return new HealthReport { Status = HealthStatus.Unknown, HttpStatusCode = 404 };

        var stopwatch = IncludeDuration ? Stopwatch.StartNew() : null;

        HealthReport healthReport = new();

        if (Data?.Count > 0)
            healthReport.Data = new Dictionary<string, object?>(Data);

        var result = await ExecuteChecksAsync(checks, stopwatch, cancellationToken);

        healthReport.Checks = [.. result
            .OrderBy(r => r.Index)
            .Select(r => r.HealthCheckEntry)];

        stopwatch?.Stop();

        if (IncludeDuration)
            healthReport.TotalDurationMs = stopwatch!.ElapsedMilliseconds;

        healthReport.Status = GetOverallStatus(healthReport.Checks.Select(c => c.Status));
        healthReport.HttpStatusCode = GetHttpStatusCode(healthReport.Status);

        return healthReport;
    }

    /// <summary>
    /// Executes all health checks concurrently while preserving original order.
    /// </summary>
    private async Task<(int Index, HealthReportEntry HealthCheckEntry)[]> ExecuteChecksAsync(
        IEnumerable<HealthCheckRegistration> checks,
        Stopwatch? stopwatch,
        CancellationToken cancellationToken)
    {
        var tasks = checks.Select(async check =>
            (check.Index, HealthCheckEntry: await ExecuteSingleCheckAsync(check, stopwatch, cancellationToken)));

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Executes a single health check and produces a report entry.
    /// </summary>
    private async Task<HealthReportEntry> ExecuteSingleCheckAsync(
        HealthCheckRegistration check,
        Stopwatch? stopwatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var start = IncludeDuration ? stopwatch!.ElapsedMilliseconds : 0;
        var entry = new HealthReportEntry { Name = check.Name };

        CancellationTokenSource? timeoutCancellationTokenSource = null;

        try
        {
            var effectiveToken = cancellationToken;

            if (check.Timeout is not null)
            {
                timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCancellationTokenSource.CancelAfter(check.Timeout.Value);
                effectiveToken = timeoutCancellationTokenSource.Token;
            }

            var result = await RunCheckAsync(check.Check, effectiveToken);

            entry.Status = result.Status;
            entry.Description = result.Description;
            SetErrorIfRequired(ref entry, exception: result.Exception);

            if (result.Data?.Count > 0)
                entry.Data = new Dictionary<string, object?>(result.Data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource?.IsCancellationRequested == true)
        {
            entry.Status = HealthStatus.Unhealthy;
            entry.Description = $"Health check timed out after {check.Timeout?.TotalMilliseconds} ms";
            SetErrorIfRequired(ref entry, errorMessage: "Timeout exceeded");
        }
        catch (Exception ex)
        {
            entry.Status = HealthStatus.Unhealthy;
            SetErrorIfRequired(ref entry, exception: ex);
        }
        finally
        {
            timeoutCancellationTokenSource?.Dispose();
        }

        if (IncludeDuration)
            entry.DurationMs = stopwatch!.ElapsedMilliseconds - start;

        entry.Tags = check.Tags;

        return entry;
    }

    /// <summary>
    /// Runs a health check on the thread pool and waits for it until the token is cancelled.
    /// </summary>
    /// <remarks>
    /// Running on the thread pool stops synchronous checks from blocking each other, and
    /// waiting on the token enforces timeouts and cancellation even for checks that ignore it.
    /// </remarks>
    private static async Task<HealthCheckResult> RunCheckAsync(IHealthCheck check, CancellationToken cancellationToken)
    {
        var task = Task.Run(() => check.CheckHealthAsync(cancellationToken), CancellationToken.None);

        try
        {
            return await task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A check that ignored cancellation may still be running; observe its eventual
            // failure so it doesn't surface as an unobserved task exception.
            _ = task.ContinueWith(
                static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            throw;
        }
    }

    private int GetHttpStatusCode(HealthStatus status)
        => status switch
        {
            HealthStatus.Healthy => HealthyHttpStatusCode,
            HealthStatus.Degraded => DegradedHttpStatusCode,
            HealthStatus.Unhealthy => UnhealthyHttpStatusCode,
            _ => UnhealthyHttpStatusCode
        };

    private void SetErrorIfRequired(ref HealthReportEntry entry, string? errorMessage = null, Exception? exception = null)
    {
        if (IncludeErrors)
        {
            if (errorMessage is not null)
            {
                entry.Error = errorMessage;
            }
            else if (exception is not null)
            {
                entry.Error = IncludeStackTrace ? exception.ToString() : exception.Message;
            }
        }
    }

    /// <summary>
    /// Internal registration record for a health check.
    /// </summary>
    internal sealed record HealthCheckRegistration(
        int Index,
        string Name,
        IHealthCheck Check,
        IReadOnlyList<string>? Tags,
        TimeSpan? Timeout);
}

/// <summary>
/// Describes a registered health check using read-only metadata
/// that can be safely exposed for filtering and selection.
/// </summary>
/// <remarks>
/// This descriptor does not allow execution or modification of the health check.
/// It is primarily used by predicate-based APIs to decide which checks should run
/// based on their name or tags.
/// </remarks>
/// <param name="Name">The unique name of the health check.</param>
/// <param name="Tags">
/// Optional tags associated with the health check.
/// Tags can be used to group and filter checks during execution.
/// </param>
public sealed record HealthCheckDescriptor(
    string Name,
    IEnumerable<string>? Tags)
{
    internal HealthCheckDescriptor(HealthChecker.HealthCheckRegistration r)
        : this(r.Name, r.Tags)
    {
    }
}