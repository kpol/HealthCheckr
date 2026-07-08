using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HealthCheckr.Func;

public class HealthFunc(ILogger<HealthFunc> logger)
{
    private readonly ILogger<HealthFunc> _logger = logger;

    [Function("Health")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
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

        // Simple sequential check returning only HealthStatus
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
    }
}

public sealed class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            Description = "Custom health check passed"
        });
    }
}