namespace HealthCheckr.Tests;

public class StatusAggregationTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public static TheoryData<HealthStatus[], HealthStatus> Statuses => new()
    {
        { [HealthStatus.Healthy, HealthStatus.Healthy], HealthStatus.Healthy },
        { [HealthStatus.Healthy, HealthStatus.Degraded], HealthStatus.Degraded },
        { [HealthStatus.Degraded, HealthStatus.Unhealthy], HealthStatus.Unhealthy },
        { [HealthStatus.Healthy, HealthStatus.Unknown], HealthStatus.Unhealthy },
        { [HealthStatus.Healthy, (HealthStatus)42], HealthStatus.Unhealthy },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task CheckAsync_AggregatesCheckStatuses(HealthStatus[] statuses, HealthStatus expected)
    {
        var report = await CreateChecker(statuses).CheckAsync(cancellationToken: TestToken);

        Assert.Equal(expected, report.Status);
    }

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task CheckSimpleAsync_AggregatesCheckStatuses(HealthStatus[] statuses, HealthStatus expected)
    {
        var status = await CreateChecker(statuses).CheckSimpleAsync(cancellationToken: TestToken);

        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task CheckAsync_CheckReturnsUnknown_KeepsEntryStatusAndUsesUnhealthyHttpStatusCode()
    {
        var report = await CreateChecker([HealthStatus.Unknown]).CheckAsync(cancellationToken: TestToken);

        Assert.Equal(HealthStatus.Unknown, Assert.Single(report.Checks).Status);
        Assert.Equal(503, report.HttpStatusCode);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, 201)]
    [InlineData(HealthStatus.Degraded, 202)]
    [InlineData(HealthStatus.Unhealthy, 500)]
    public async Task CheckAsync_UsesConfiguredHttpStatusCodes(HealthStatus status, int expectedHttpStatusCode)
    {
        HealthChecker healthChecker = new()
        {
            HealthyHttpStatusCode = 201,
            DegradedHttpStatusCode = 202,
            UnhealthyHttpStatusCode = 500
        };
        healthChecker.AddCheck("check", () => Task.FromResult(new HealthCheckResult { Status = status }));

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(expectedHttpStatusCode, report.HttpStatusCode);
    }

    [Fact]
    public async Task CheckAsync_NoMatchingChecks_ReturnsUnknownWith404()
    {
        var report = await CreateChecker([HealthStatus.Healthy]).CheckAsync("missing", TestToken);

        Assert.Equal(HealthStatus.Unknown, report.Status);
        Assert.Equal(404, report.HttpStatusCode);
        Assert.Empty(report.Checks);
    }

    [Fact]
    public async Task CheckSimpleAsync_NoMatchingChecks_ReturnsUnknown()
    {
        var status = await CreateChecker([HealthStatus.Healthy]).CheckSimpleAsync("missing", TestToken);

        Assert.Equal(HealthStatus.Unknown, status);
    }

    private static HealthChecker CreateChecker(HealthStatus[] statuses)
    {
        HealthChecker healthChecker = new();

        for (int i = 0; i < statuses.Length; i++)
        {
            var status = statuses[i];
            healthChecker.AddCheck($"check {i}", () => Task.FromResult(new HealthCheckResult { Status = status }));
        }

        return healthChecker;
    }
}
