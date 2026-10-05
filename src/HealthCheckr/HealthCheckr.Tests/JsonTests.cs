using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HealthCheckr.Responses;

namespace HealthCheckr.Tests;

// The JSON is the contract consumers parse, so these tests pin its exact shape.
public partial class JsonTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToJson_ProducesDocumentedShape()
    {
        var report = await CreateReportAsync(includeDuration: false);

        var json = TimestampValue().Replace(report.ToJson(), "\"timestamp\": \"<timestamp>\"");

        const string expected = """
            {
              "status": "Unhealthy",
              "checks": [
                {
                  "name": "db",
                  "status": "Healthy",
                  "tags": [
                    "critical"
                  ]
                },
                {
                  "name": "api",
                  "status": "Degraded",
                  "description": "Slow responses",
                  "data": {
                    "LatencyMs": 812
                  }
                },
                {
                  "name": "queue",
                  "status": "Unhealthy",
                  "error": "Queue unreachable"
                }
              ],
              "timestamp": "<timestamp>",
              "data": {
                "Environment": "Production"
              }
            }
            """;

        Assert.Equal(expected.ReplaceLineEndings("\n"), json.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task ToJson_TimestampIsUtcWithSevenFractionalDigits()
    {
        var report = await CreateReportAsync(includeDuration: false);

        using var document = JsonDocument.Parse(report.ToJson());

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$", document.RootElement.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task ToJson_IncludeDuration_WritesDurations()
    {
        var report = await CreateReportAsync(includeDuration: true);

        using var document = JsonDocument.Parse(report.ToJson());

        Assert.True(document.RootElement.GetProperty("totalDurationMs").GetInt64() >= 0);
        Assert.All(document.RootElement.GetProperty("checks").EnumerateArray(),
            check => Assert.True(check.GetProperty("durationMs").GetInt64() >= 0));
    }

    [Fact]
    public async Task ToJsonAsync_WritesSameJsonAsToJson()
    {
        var report = await CreateReportAsync(includeDuration: true);
        using var stream = new MemoryStream();

        await report.ToJsonAsync(stream, TestToken);

        Assert.Equal(report.ToJson(), Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static Task<HealthReport> CreateReportAsync(bool includeDuration)
    {
        HealthChecker healthChecker = new()
        {
            IncludeDuration = includeDuration,
            Data = new Dictionary<string, object?> { ["Environment"] = "Production" }
        };

        healthChecker.AddCheck("db", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: ["critical"]);
        healthChecker.AddCheck("api", static () => Task.FromResult(HealthCheckResult.Degraded(
            description: "Slow responses",
            data: new Dictionary<string, object?> { ["LatencyMs"] = 812 })));
        healthChecker.AddCheck("queue", static ct => throw new InvalidOperationException("Queue unreachable"));

        return healthChecker.CheckAsync(cancellationToken: TestToken);
    }

    [GeneratedRegex("\"timestamp\": \"[^\"]+\"")]
    private static partial Regex TimestampValue();
}
