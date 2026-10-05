namespace HealthCheckr.Tests;

public class FailureHandlingTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // HttpClient throws TaskCanceledException when its own HttpClient.Timeout elapses,
    // even though neither the caller's token nor the check's timeout was cancelled.
    private static async Task<HealthCheckResult> ThrowUnrelatedCancellation(CancellationToken _)
    {
        await Task.Yield();
        throw new TaskCanceledException("HttpClient.Timeout elapsed", new TimeoutException());
    }

    [Fact]
    public async Task CheckAsync_CheckThrowsUnrelatedCancellation_ReportsCheckAsUnhealthy()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("ok", static () => Task.FromResult(HealthCheckResult.Healthy()));
        healthChecker.AddCheck("http", ThrowUnrelatedCancellation);

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Collection(report.Checks,
            entry => Assert.Equal(HealthStatus.Healthy, entry.Status),
            entry =>
            {
                Assert.Equal(HealthStatus.Unhealthy, entry.Status);
                Assert.Equal("HttpClient.Timeout elapsed", entry.Error);
            });
    }

    [Fact]
    public async Task CheckSimpleAsync_CheckThrowsUnrelatedCancellation_ReturnsUnhealthy()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("http", ThrowUnrelatedCancellation);

        var status = await healthChecker.CheckSimpleAsync(cancellationToken: TestToken);

        Assert.Equal(HealthStatus.Unhealthy, status);
    }

    [Fact]
    public async Task CheckAsync_CheckThrowsSynchronously_ReportsCheckAsUnhealthy()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("throws", static ct => throw new InvalidOperationException("Boom"));

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        var entry = Assert.Single(report.Checks);
        Assert.Equal(HealthStatus.Unhealthy, entry.Status);
        Assert.Equal("Boom", entry.Error);
        Assert.Equal(503, report.HttpStatusCode);
    }

    [Fact]
    public async Task CheckAsync_IncludeErrorsDisabled_OmitsError()
    {
        HealthChecker healthChecker = new() { IncludeErrors = false };
        healthChecker.AddCheck("throws", static ct => throw new InvalidOperationException("Boom"));

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Null(Assert.Single(report.Checks).Error);
    }

    [Fact]
    public async Task CheckAsync_CallerCancels_Throws()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("ok", static () => Task.FromResult(HealthCheckResult.Healthy()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => healthChecker.CheckAsync(cancellationToken: new CancellationToken(canceled: true)));
    }
}
