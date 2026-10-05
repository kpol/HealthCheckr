namespace HealthCheckr.Tests;

public class CachingTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckAsync_WithoutCacheDuration_RunsCheckEveryTime()
    {
        var check = new CountingCheck(() => HealthCheckResult.Healthy());
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("check", check);

        await healthChecker.CheckAsync(cancellationToken: TestToken);
        await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(2, check.Runs);
    }

    [Fact]
    public async Task CheckAsync_WithCacheDuration_ReusesResult()
    {
        var check = new CountingCheck(() => HealthCheckResult.Degraded("Slow"));
        HealthChecker healthChecker = new() { CacheDuration = TimeSpan.FromHours(1) };
        healthChecker.AddCheck("check", check);

        var first = Assert.Single((await healthChecker.CheckAsync(cancellationToken: TestToken)).Checks);
        var second = Assert.Single((await healthChecker.CheckAsync(cancellationToken: TestToken)).Checks);

        Assert.Equal(1, check.Runs);
        Assert.Equal(HealthStatus.Degraded, second.Status);
        Assert.Equal("Slow", second.Description);
        Assert.Equal(first.DurationMs, second.DurationMs);
    }

    [Fact]
    public async Task CheckAsync_WithCacheDuration_CachesFailures()
    {
        var check = new CountingCheck(() => throw new InvalidOperationException("Down"));
        HealthChecker healthChecker = new() { CacheDuration = TimeSpan.FromHours(1) };
        healthChecker.AddCheck("check", check);

        await healthChecker.CheckAsync(cancellationToken: TestToken);
        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(1, check.Runs);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal("Down", Assert.Single(report.Checks).Error);
    }

    [Fact]
    public async Task CheckAsync_CacheDurationElapsed_RunsCheckAgain()
    {
        var check = new CountingCheck(() => HealthCheckResult.Healthy());
        HealthChecker healthChecker = new() { CacheDuration = TimeSpan.FromMilliseconds(100) };
        healthChecker.AddCheck("check", check);

        await healthChecker.CheckAsync(cancellationToken: TestToken);
        await Task.Delay(300, TestToken);
        await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(2, check.Runs);
    }

    [Fact]
    public async Task CacheDuration_IsSharedByAllOverloads()
    {
        var check = new CountingCheck(() => HealthCheckResult.Healthy());
        HealthChecker healthChecker = new() { CacheDuration = TimeSpan.FromHours(1) };
        healthChecker.AddCheck("check", check, tags: ["tag"]);

        await healthChecker.CheckSimpleAsync(cancellationToken: TestToken);
        await healthChecker.CheckAsync(cancellationToken: TestToken);
        await healthChecker.CheckAsync("check", TestToken);
        await healthChecker.CheckAsync(includeTags: ["tag"], cancellationToken: TestToken);
        await healthChecker.CheckAsync(_ => true, TestToken);

        Assert.Equal(1, check.Runs);
    }

    [Fact]
    public async Task CheckAsync_EntryData_IsReadOnly()
    {
        HealthChecker healthChecker = new() { CacheDuration = TimeSpan.FromHours(1) };
        healthChecker.AddCheck("check", new CountingCheck(() =>
            HealthCheckResult.Healthy(data: new Dictionary<string, object?> { ["Version"] = "1.2" })));

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        var data = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(report.Checks).Data);
        Assert.True(data.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => data["Version"] = "changed");
    }

    private sealed class CountingCheck(Func<HealthCheckResult> getResult) : IHealthCheck
    {
        private int _runs;

        public int Runs => Volatile.Read(ref _runs);

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _runs);
            return Task.FromResult(getResult());
        }
    }
}
