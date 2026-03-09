using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class ReleaseNeverChangesAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static readonly DateTime Now = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Analyze_LessThan14DaysHistory_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Event from 5 days ago (not enough history)
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Now.AddDays(-5));
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: Now.AddDays(-5));
        var analyzer = new ReleaseNeverChangesAnalyzer(ctx, AnalyzerTestHelper.StubDateTime(Now));

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_SingleRecentRelease_Over14DaysHistory_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Event from 20 days ago
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Now.AddDays(-20));
        // One release in last 30 days
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: Now.AddDays(-15));
        var analyzer = new ReleaseNeverChangesAnalyzer(ctx, AnalyzerTestHelper.StubDateTime(Now));

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("release-never-changes");
    }

    [Fact]
    public async Task Analyze_MultipleRecentReleases_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Now.AddDays(-20));
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: Now.AddDays(-10));
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v2-{Guid.NewGuid():N}", firstSeen: Now.AddDays(-5));
        var analyzer = new ReleaseNeverChangesAnalyzer(ctx, AnalyzerTestHelper.StubDateTime(Now));

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }
}
