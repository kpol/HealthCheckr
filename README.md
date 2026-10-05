# HealthCheckr

A lightweight health check library for .NET and Azure Functions that runs asynchronous checks in parallel, preserves execution order and returns a clean JSON response with status, durations and optional error details.

The library is framework-agnostic and has no dependency on ASP.NET, making it suitable for console apps, background services, Azure Functions and any .NET runtime.

[![CI Build](https://github.com/kpol/HealthCheckr/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/kpol/HealthCheckr/actions/workflows/dotnetcore.yml)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckr.svg?logo=nuget)](https://www.nuget.org/packages/HealthCheckr)

## Features

- Async and concurrent execution with cancellation support
- Order-preserving results for consistent logging and dashboards
- Configurable HTTP return codes and optional diagnostics
- Attach arbitrary metadata at the global or per-check level (for example region, version, dependency info)
- Tag-based filtering with include and exclude semantics
- Per-check timeouts, enforced even for checks that ignore cancellation
- Optional result caching to limit the load frequent probes put on your dependencies
- Works well in Azure Functions, serverless, worker services and web APIs
- Minimal dependencies and easy to integrate
- Lightweight `CheckSimpleAsync` that returns only the overall `HealthStatus` and stops at the first failure

## Installation

Install via NuGet:

```bash
dotnet add package HealthCheckr
```

Or via the NuGet Package Manager:

```
PM> Install-Package HealthCheckr
```


## Usage

Create one `HealthChecker`, register its checks at startup and reuse it for every request, for example as a singleton. Running checks from several requests at once is safe; registering checks while checks are running is not.

```csharp
using HealthCheckr;

HealthChecker healthChecker = new()
{
    IncludeErrors = true,
    IncludeStackTrace = true,
    Data = new Dictionary<string, object?>
    {
        ["Environment"] = "Production",
        ["Id"] = 42
    }
};

healthChecker.AddCheck("Check 1",
    static () => Task.FromResult(HealthCheckResult.Healthy())
);

healthChecker.AddCheck("Check 2",
    static ct =>
    {
        return Task.FromResult(HealthCheckResult.Degraded(
            description: "Check 2 is degraded.",
            data: new Dictionary<string, object?> { ["Metadata1"] = 123 }));
    },
    tags: ["external"]
);

healthChecker.AddCheck("Check 3",
    static async ct =>
    {
        await Task.Delay(2000, ct);
        return HealthCheckResult.Healthy();
    },
    tags: ["external"],
    timeout: TimeSpan.FromMilliseconds(500)
);

healthChecker.AddCheck("Check 4",
    new CustomHealthCheck(), // Implements IHealthCheck interface
    tags: ["external", "critical"]
);

// Full JSON health report
var result = await healthChecker.CheckAsync(includeTags: ["external"]);

// Overall status only, without a report; returns as soon as any check fails
var simpleStatus = await healthChecker.CheckSimpleAsync(
    includeTags: ["external"],
    excludeTags: null);

Console.WriteLine(simpleStatus);

return new ContentResult
{
    Content = result.ToJson(),
    ContentType = "application/json",
    StatusCode = result.HttpStatusCode
};
```

## Tag filtering semantics

HealthCheckr supports include and exclude tag filters to control which checks run.

### Rules

1. **Exclude always wins**  
   If a check has any excluded tag it will not run even if it also matches include tags.

2. **Include acts as an allow list**  
   When include tags are specified only checks that contain at least one included tag will run.

3. **Untagged checks are excluded when filters are present**  
   If a check has no tags it will only run when no include or exclude filters are provided.

4. **Tags are case-insensitive**  
   `External` and `external` are the same tag, both when registering checks and when filtering.


## Status and HTTP codes

- The overall status is the worst status among the checks that ran: any Unhealthy check makes the report Unhealthy, otherwise any Degraded check makes it Degraded.
- A check that throws is reported as Unhealthy, with the exception message as its `error`.
- A check that returns `Unknown` counts as Unhealthy for the overall status; its own entry still shows `Unknown`.
- If no checks match (for example an unknown check name, or tag filters that match nothing), the status is `Unknown` and `HttpStatusCode` is `404`.


## Timeouts

Pass `timeout` when registering a check. If the check runs longer, it is reported as Unhealthy with the description `Health check timed out after <n> ms` and the error `Timeout exceeded`.

The timeout is enforced even if the check ignores its `CancellationToken`. The check should still pass the token to the I/O it does, for example `HttpClient` calls, so that the underlying work actually stops. A check that ignores the token keeps running in the background after it times out; the next call waits for that run (up to its own timeout) instead of starting another copy, so a hung dependency can't pile up work.

The `AddCheck` overload that takes a `Func<Task<HealthCheckResult>>` without a token doesn't accept a timeout.


## Caching

Set `CacheDuration` to reuse each check's result instead of running the check on every call:

```csharp
HealthChecker healthChecker = new()
{
    CacheDuration = TimeSpan.FromSeconds(10)
};
```

Every outcome is cached per check, including failures and timeouts, and the cache is shared by all `CheckAsync` and `CheckSimpleAsync` overloads. Cached entries keep the `durationMs` of the run that produced them, while the report's `timestamp` is always the time of the call. Caching is off by default.


## Example JSON Output

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "Check 2",
      "status": "Degraded",
      "description": "Check 2 is degraded.",
      "durationMs": 6,
      "data": {
        "Metadata1": 123
      },
      "tags": [
        "external"
      ]
    },
    {
      "name": "Check 3",
      "status": "Unhealthy",
      "description": "Health check timed out after 500 ms",
      "error": "Timeout exceeded",
      "durationMs": 511,
      "tags": [
        "external"
      ]
    },
    {
      "name": "Check 4",
      "status": "Healthy",
      "description": "Custom health check passed",
      "durationMs": 1,
      "tags": [
        "external",
        "critical"
      ]
    }
  ],
  "totalDurationMs": 518,
  "timestamp": "2026-10-05T01:33:07.2375846Z",
  "data": {
    "Environment": "Production",
    "Id": 42
  }
}
```

The `timestamp` is always UTC. Properties with no value, such as a missing `description` or `error`, are omitted.


## Configuration

| Property | Default | Description |
| --- | --- | --- |
| `HealthyHttpStatusCode` | `200` | `HttpStatusCode` when the overall status is Healthy |
| `DegradedHttpStatusCode` | `200` | `HttpStatusCode` when the overall status is Degraded |
| `UnhealthyHttpStatusCode` | `503` | `HttpStatusCode` when the overall status is Unhealthy |
| `IncludeErrors` | `true` | Include the error message of failing checks as `error` |
| `IncludeStackTrace` | `false` | Include the full exception, including its stack trace, instead of only the message. Applies only when `IncludeErrors` is on |
| `IncludeDuration` | `true` | Include `durationMs` for each check and `totalDurationMs` for the report |
| `Data` | `null` | Metadata added to every report |
| `CacheDuration` | `null` | How long each check's result is reused; see [Caching](#caching) |

Error messages often contain host names or connection details. If your health endpoint is publicly reachable, consider setting `IncludeErrors = false` or restricting access to the endpoint.


## Upgrading from 2.x

Breaking changes in 3.0:

- `HealthChecker.Data` is now `IReadOnlyDictionary<string, object?>?` (was `Dictionary<string, object?>?`).
- `HealthReportEntry.Tags` is now `IEnumerable<string>?` (was `string[]?`).
- `IncludeErrors` now defaults to `true` (was `false`). Set it to `false` to keep the 2.x output.
- `timestamp` is written with a `Z` suffix, for example `2026-10-05T01:33:07.2375846Z` (was `+00:00`).
- Tag matching is case-insensitive.
- A check that returns `Unknown` makes the overall status Unhealthy.
- `CheckSimpleAsync` runs checks concurrently and returns at the first failure (was one check at a time).
- Checks run on the thread pool, and timeouts are enforced even for checks that ignore cancellation.
- A check that throws `OperationCanceledException` without the call being cancelled, for example when an `HttpClient` timeout elapses, is reported as Unhealthy instead of making `CheckAsync` throw.

## License

&copy; Kirill Polishchuk
