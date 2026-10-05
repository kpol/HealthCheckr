namespace HealthCheckr.Tests;

public class TagTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckAsync_ReportTags_AreReadOnly()
    {
        var healthChecker = CreateChecker();

        var report = await healthChecker.CheckAsync("db", TestToken);

        var tags = Assert.IsAssignableFrom<ICollection<string>>(Assert.Single(report.Checks).Tags);
        Assert.True(tags.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => tags.Add("injected"));
        Assert.Empty((await healthChecker.CheckAsync(includeTags: ["injected"], cancellationToken: TestToken)).Checks);
    }

    [Fact]
    public async Task CheckAsync_DescriptorTags_AreReadOnly()
    {
        var healthChecker = CreateChecker();
        List<IEnumerable<string>?> descriptorTags = [];

        await healthChecker.CheckAsync(d =>
        {
            descriptorTags.Add(d.Tags);
            return false;
        }, TestToken);

        var taggedDescriptors = descriptorTags.OfType<IEnumerable<string>>().ToList();
        Assert.Equal(3, taggedDescriptors.Count);
        Assert.All(taggedDescriptors, tags => Assert.True(Assert.IsAssignableFrom<ICollection<string>>(tags).IsReadOnly));
    }

    [Fact]
    public async Task AddCheck_Tags_KeepRegistrationOrderWithoutDuplicates()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("check", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: ["b", "a", "b"]);

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(["b", "a"], Assert.Single(report.Checks).Tags!);
    }

    [Fact]
    public async Task AddCheck_EmptyTags_AreTreatedAsUntagged()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("check", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: []);

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Null(Assert.Single(report.Checks).Tags);
    }

    // Rules documented in the README under "Tag filtering semantics"
    [Theory]
    [InlineData(null, null, new[] { "untagged", "db", "api", "slow" })]
    [InlineData(new string[0], new string[0], new[] { "untagged", "db", "api", "slow" })]
    [InlineData(new[] { "external" }, null, new[] { "api", "slow" })]
    [InlineData(new[] { "critical", "external" }, null, new[] { "db", "api", "slow" })]
    [InlineData(new[] { "external" }, new[] { "slow" }, new[] { "api" })]
    [InlineData(null, new[] { "slow" }, new[] { "db", "api" })]
    public async Task CheckAsync_FiltersByTags(string[]? includeTags, string[]? excludeTags, string[] expected)
    {
        var report = await CreateChecker().CheckAsync(includeTags, excludeTags, TestToken);

        Assert.Equal(expected, report.Checks.Select(c => c.Name));
    }

    [Fact]
    public async Task CheckAsync_TagFilters_IgnoreCase()
    {
        var report = await CreateChecker().CheckAsync(includeTags: ["EXTERNAL"], excludeTags: ["Slow"], cancellationToken: TestToken);

        Assert.Equal(["api"], report.Checks.Select(c => c.Name));
    }

    [Fact]
    public async Task AddCheck_TagsDifferingOnlyByCase_KeepFirstSpelling()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("check", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: ["Db", "db", "DB"]);

        var report = await healthChecker.CheckAsync(cancellationToken: TestToken);

        Assert.Equal(["Db"], Assert.Single(report.Checks).Tags!);
    }

    [Fact]
    public async Task CheckAsync_Predicate_SelectsMatchingChecks()
    {
        var report = await CreateChecker().CheckAsync(d => d.Tags?.Contains("external") == true, TestToken);

        Assert.Equal(["api", "slow"], report.Checks.Select(c => c.Name));
    }

    [Fact]
    public void AddCheck_DuplicateNameWithDifferentCase_Throws()
    {
        var healthChecker = CreateChecker();

        Assert.Throws<ArgumentException>(
            () => healthChecker.AddCheck("DB", static () => Task.FromResult(HealthCheckResult.Healthy())));
    }

    private static HealthChecker CreateChecker()
    {
        HealthChecker healthChecker = new();
        healthChecker.AddCheck("untagged", static () => Task.FromResult(HealthCheckResult.Healthy()));
        healthChecker.AddCheck("db", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: ["critical", "db"]);
        healthChecker.AddCheck("api", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: ["external"]);
        healthChecker.AddCheck("slow", static () => Task.FromResult(HealthCheckResult.Healthy()), tags: ["external", "slow"]);
        return healthChecker;
    }
}
