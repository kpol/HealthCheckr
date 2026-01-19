# HealthCheckr

A lightweight, async-friendly health check library for .NET and Azure Functions.
Runs multiple health checks in parallel, preserves execution order, and produces a clean JSON response with status, durations, and optional error details.

---

## Features

* Async health checks with support for `CancellationToken`
* Preserve registration order in JSON response
* Optional duration tracking per check
* Optional error reporting per check
* Configurable HTTP status codes for Healthy, Degraded, and Unhealthy states
* JSON output with enum status serialized as string
* Compatible with .NET 8+ (future .NET 10 support)

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
  "status": "Degraded",
  "checks": [
    {
      "name": "Database",
      "description": "Checks database connectivity",
      "status": "Healthy",
      "durationMs": 12
    },
    {
      "name": "Cache",
      "description": "Checks Redis cache availability",
      "status": "Degraded",
      "durationMs": 8
    }
  ],
  "totalDurationMs": 20,
  "timestamp": "2026-01-19T11:15:23.123Z"
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
