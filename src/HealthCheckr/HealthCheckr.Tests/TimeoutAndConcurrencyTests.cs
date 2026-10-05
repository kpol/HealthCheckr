using HealthCheckr.Responses;

namespace HealthCheckr.Tests;

// Timeouts are set on these tests so a regression fails the test instead of hanging the run.
public class TimeoutAndConcurrencyTests
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMilliseconds(100);

    [Fact(Timeout = 10_000)]
    public async Task CheckAsync_CooperativeCheckExceedsTimeout_ReportsTimeout()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("slow", static async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return HealthCheckResult.Healthy();
        }, timeout: CheckTimeout);

        var report = await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken);

        AssertTimedOut(report);
    }

    [Fact(Timeout = 10_000)]
    public async Task CheckAsync_CheckIgnoresTokenAndExceedsTimeout_ReportsTimeout()
    {
        var hang = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("hangs", _ => hang.Task, timeout: CheckTimeout);

        try
        {
            var report = await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken);

            AssertTimedOut(report);
        }
        finally
        {
            hang.TrySetResult(HealthCheckResult.Healthy());
        }
    }

    [Fact(Timeout = 10_000)]
    public async Task CheckAsync_BlockingCheckExceedsTimeout_ReportsTimeout()
    {
        // Not disposed: the blocked check may still be waking up when the test ends
        var release = new ManualResetEventSlim();
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("blocks", _ =>
        {
            release.Wait(TestContext.Current.CancellationToken);
            return Task.FromResult(HealthCheckResult.Healthy());
        }, timeout: CheckTimeout);

        try
        {
            var report = await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken);

            AssertTimedOut(report);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact(Timeout = 10_000)]
    public async Task CheckSimpleAsync_CheckIgnoresTokenAndExceedsTimeout_ReturnsUnhealthy()
    {
        var hang = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("hangs", _ => hang.Task, timeout: CheckTimeout);

        try
        {
            var status = await healthChecker.CheckSimpleAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(HealthStatus.Unhealthy, status);
        }
        finally
        {
            hang.TrySetResult(HealthCheckResult.Healthy());
        }
    }

    [Fact(Timeout = 10_000)]
    public async Task CheckAsync_CallerCancelsWhileCheckIgnoresToken_Throws()
    {
        var hang = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("hangs", () => hang.Task);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(CheckTimeout);

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => healthChecker.CheckAsync(cancellationToken: cts.Token));
        }
        finally
        {
            hang.TrySetResult(HealthCheckResult.Healthy());
        }
    }

    [Fact(Timeout = 10_000)]
    public async Task CheckSimpleAsync_CallerCancelsWhileCheckIgnoresToken_Throws()
    {
        var hang = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("hangs", () => hang.Task);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(CheckTimeout);

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => healthChecker.CheckSimpleAsync(cancellationToken: cts.Token));
        }
        finally
        {
            hang.TrySetResult(HealthCheckResult.Healthy());
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task CheckAsync_BlockingChecks_RunConcurrently()
    {
        using var barrier = new Barrier(2);
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("a", () => Task.FromResult(Rendezvous(barrier)));
        healthChecker.AddCheck("b", () => Task.FromResult(Rendezvous(barrier)));

        var report = await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task CheckSimpleAsync_BlockingChecks_RunConcurrently()
    {
        using var barrier = new Barrier(2);
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("a", () => Task.FromResult(Rendezvous(barrier)));
        healthChecker.AddCheck("b", () => Task.FromResult(Rendezvous(barrier)));

        var status = await healthChecker.CheckSimpleAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, status);
    }

    [Fact]
    public async Task CheckAsync_ChecksCompleteOutOfOrder_PreservesRegistrationOrder()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("first", static async ct =>
        {
            await Task.Delay(200, ct);
            return HealthCheckResult.Healthy();
        });
        healthChecker.AddCheck("second", static () => Task.FromResult(HealthCheckResult.Healthy()));

        var report = await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], report.Checks.Select(c => c.Name));
    }

    [Fact(Timeout = 10_000)]
    public async Task CheckAsync_EarlierRunStillInProgress_WaitsForItInsteadOfStartingAnother()
    {
        var hang = new TaskCompletionSource<HealthCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("hangs", _ =>
        {
            Interlocked.Increment(ref runs);
            started.TrySetResult();
            return hang.Task;
        }, timeout: CheckTimeout);

        try
        {
            AssertTimedOut(await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken));
            await started.Task.WaitAsync(TestContext.Current.CancellationToken);

            AssertTimedOut(await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(HealthStatus.Unhealthy, await healthChecker.CheckSimpleAsync(cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal(1, Volatile.Read(ref runs));
        }
        finally
        {
            hang.TrySetResult(HealthCheckResult.Healthy());
        }

        // Once the stuck run finishes, the check reports normally again
        HealthReport report;
        do
        {
            report = await healthChecker.CheckAsync(cancellationToken: TestContext.Current.CancellationToken);
        }
        while (report.Status != HealthStatus.Healthy);
    }

    // Blocks until the other check arrives, which only happens if both checks run at the same time
    private static HealthCheckResult Rendezvous(Barrier barrier) =>
        barrier.SignalAndWait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The other check never started");

    private static void AssertTimedOut(HealthReport report)
    {
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        var entry = Assert.Single(report.Checks);
        Assert.Equal(HealthStatus.Unhealthy, entry.Status);
        Assert.Equal("Health check timed out after 100 ms", entry.Description);
        Assert.Equal("Timeout exceeded", entry.Error);
    }
}
