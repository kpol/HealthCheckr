using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HealthCheckr.Func;

public class HealthFunc
{
    private readonly ILogger<HealthFunc> _logger;

    public HealthFunc(ILogger<HealthFunc> logger)
    {
        _logger = logger;
    }

    [Function("Health")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        HealthChecker healthChecker = new()
        {
            IncludeErrors = true,
            Data = new Dictionary<string, object?>
            {
                ["Environment"] = "Production",
                ["Id"] = 42
            }
        };

        // Add checks
        healthChecker.AddCheck("Check 1",
            async () =>
            {
                return await Task.FromResult(new HealthCheckResult { Status = HealthStatus.Healthy });
            }
        );

        healthChecker.AddCheck("Check 2",
            async () =>
            {
                return await Task.FromResult(new HealthCheckResult
                {
                    Status = HealthStatus.Degraded,
                    Data = new Dictionary<string, object?> { ["Metadata1"] = 123 }
                });
            },
            tags: ["external"]
        );

        healthChecker.AddCheck("Check 3",
            async () =>
            {
                return await Task.FromResult(new HealthCheckResult { Status = HealthStatus.Unhealthy });
            },
            tags: ["external", "critical"]
        );

        // Full JSON health report
        var result = await healthChecker.CheckAsync(
            includeTags: ["external"]
        );



        // Simple sequential check returning only HealthStatus
        var simpleStatus = await healthChecker.CheckSimpleAsync(
            includeTags: ["external"],
            excludeTags: null
        );

        Console.WriteLine(simpleStatus);

        return new ContentResult
        {
            Content = result.ToJson(),
            ContentType = "application/json",
            StatusCode = result.HttpStatusCode
        };
    }
}