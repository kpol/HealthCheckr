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
            }
        );

        healthChecker.AddCheck("Check 3",
            async () =>
            {
                return await Task.FromResult(HealthStatus.Unhealthy);
            }
        );

        var result = await healthChecker.CheckAsync();

        return new ContentResult
        {
            Content = result.ToJson(),
            ContentType = "application/json",
            StatusCode = result.HttpStatusCode
        };
    }
}