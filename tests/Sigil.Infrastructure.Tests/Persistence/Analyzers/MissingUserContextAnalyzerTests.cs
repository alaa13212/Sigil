using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class MissingUserContextAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_TooFewEvents_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 10);
        var analyzer = new MissingUserContextAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_MostEventsHaveNoUser_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // 25 events without user context
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 25);
        var analyzer = new MissingUserContextAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("missing-user-context");
    }

    [Fact]
    public async Task Analyze_OverTenPercentHaveUser_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var eventUser = await TestHelper.CreateEventUserAsync(ctx, "user1");
        // 20 events without user
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 18);
        // 5 events with user (> 10%)
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 5, userId: eventUser.UniqueIdentifier);
        var analyzer = new MissingUserContextAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }
}
