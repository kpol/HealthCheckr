# HealthCheckr

Lightweight async health checks for .NET and Azure Functions with ordered results and clean JSON output.

[![CI Build](https://github.com/kpol/HealthCheckr/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/kpol/HealthCheckr/actions/workflows/dotnetcore.yml)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckr.svg?logo=nuget)](https://www.nuget.org/packages/HealthCheckr)

## Features

- Async and concurrent execution with cancellation support
- Order-preserving results for consistent logging and dashboards
- Configurable HTTP return codes and optional diagnostics
- Attach arbitrary metadata at the global or per-check level (for example region, version, dependency info)
- Tag-based filtering with include and exclude semantics
- Works well in Azure Functions, serverless, worker services and web APIs
- Minimal dependencies and easy to integrate

---

## Installation

Install via NuGet:

```bash
dotnet add package HealthCheckr
```

Or via the NuGet Package Manager:

```
PM> Install-Package HealthCheckr
```

---

## Usage

```csharp
using HealthCheckr;

HealthChecker healthChecker = new()
{
    IncludeErrors = true,
    Metadata = new Dictionary<string, object?>
    {
        ["Environment"] = "Production",
        ["Id"] = 42
    }
};

healthChecker.AddCheck("Check 1",
    async () =>
    {
        return await Task.FromResult(HealthStatus.Healthy);
    }
);

healthChecker.AddCheck("Check 2",
    async () =>
    {
        return await Task.FromResult(HealthStatus.Degraded);
    },
    metadata: new Dictionary<string, object?>
    {
        ["Metadata1"] = 123
    },
    tags: ["external"]
);

healthChecker.AddCheck("Check 3",
    async () =>
    {
        return await Task.FromResult(HealthStatus.Unhealthy);
    },
    tags: ["external", "critical"]
);

var result = await healthChecker.CheckAsync(
    includeTags: ["external"],
    excludeTags: null
);

Console.WriteLine(result.ToJson());
```

---

## Tag filtering semantics

HealthCheckr supports include and exclude tag filters to control which checks run.

### Rules

1. **Exclude always wins**  
   If a check has any excluded tag it will not run even if it also matches include tags.

2. **Include acts as an allow list**  
   When include tags are specified only checks that contain at least one included tag will run.

3. **Untagged checks are excluded when filters are present**  
   If a check has no tags it will only run when no include or exclude filters are provided.

---

## Example JSON Output

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "Check 2",
      "status": "Degraded",
      "durationMs": 3,
      "metadata": {
        "Metadata1": 123
      },
      "tags": [
        "external"
      ]
    },
    {
      "name": "Check 3",
      "status": "Unhealthy",
      "durationMs": 1,
      "tags": [
        "external",
        "critical"
      ]
    }
  ],
  "totalDurationMs": 19,
  "timestamp": "2026-01-20T03:24:09.06152+00:00",
  "metadata": {
    "Environment": "Production",
    "Id": 42
  }
}
```

---

## Configuration

- `IncludeErrors` &ndash; Include exception details for failing checks
- `IncludeDuration` &ndash; Include execution duration per check
- `HealthyHttpStatusCode` / `DegradedHttpStatusCode` / `UnhealthyHttpStatusCode` &ndash; Customize HTTP response codes for the overall status

---

## Contributing

Contributions are welcome. Please open an issue or pull request on GitHub.

---

## License

&copy; Kirill Polishchuk
