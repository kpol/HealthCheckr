# HealthCheckr

Lightweight async health checks for .NET and Azure Functions with ordered results and clean JSON output.

[![CI Build](https://github.com/kpol/HealthCheckr/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/kpol/HealthCheckr/actions/workflows/dotnetcore.yml)
[![Nuget](https://img.shields.io/nuget/v/HealthCheckr.svg?logo=nuget)](https://www.nuget.org/packages/HealthCheckr)

## Features

- Async + concurrent execution with cancellation support
- Order-preserving results (useful for consistent logging and dashboards)
- Configurable HTTP return codes and optional diagnostics
- Attach arbitrary metadata at the global or per-check level (e.g. region, version, dependency info)
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

var checker = new HealthChecker()
    .AddCheck("Database", async ct => {
        // perform your database check
        return HealthStatus.Healthy;
    }, "Checks database connectivity")
    .AddCheck("Cache", async ct => {
        // perform cache check
        return HealthStatus.Degraded;
    }, "Checks Redis cache availability");

var result = await checker.CheckAsync();

Console.WriteLine(result.ToJson());
```

### Example JSON Output

```json

{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "Check 1",
      "status": "Healthy",
      "durationMs": 3
    },
    {
      "name": "Check 2",
      "status": "Degraded",
      "durationMs": 1,
      "metadata": {
        "Metadata1": 123
      }
    },
    {
      "name": "Check 3",
      "status": "Unhealthy",
      "durationMs": 1
    }
  ],
  "totalDurationMs": 21,
  "timestamp": "2026-01-19T21:34:30.9969944+00:00",
  "metadata": {
    "Environment": "Production",
    "Id": 42
  }
}
```

---

## Configuration

* `IncludeErrors` &ndash; Include exception details for failing checks.
* `IncludeDuration` &ndash; Include execution duration per check.
* `HealthyHttpStatusCode` / `DegradedHttpStatusCode` / `UnhealthyHttpStatusCode` &ndash; Customize HTTP response codes for overall status.

---

## Contributing

Contributions are welcome. Please open an issue or pull request on GitHub.

---

## License

&copy; Kirill Polishchuk
