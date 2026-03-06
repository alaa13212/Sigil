using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class HighVolumeNoGroupingAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_TooFewIssues_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        // only 5 issues
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateIssueAsync(ctx, project.Id);
        var analyzer = new HighVolumeNoGroupingAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_MostIssuesSingleEvent_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        // 25 issues, all with OccurrenceCount=1 (default from CreateIssueAsync)
        for (int i = 0; i < 25; i++)
            await TestHelper.CreateIssueAsync(ctx, project.Id);
        var analyzer = new HighVolumeNoGroupingAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("high-volume-no-grouping");
    }

    [Fact]
    public async Task Analyze_GoodGrouping_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        // 25 issues, most with multiple events
        for (int i = 0; i < 25; i++)
        {
            var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
            var tracked = await ctx.Issues.FindAsync(issue.Id);
            tracked!.OccurrenceCount = 10; // multiple events
            await ctx.SaveChangesAsync();
        }
        var analyzer = new HighVolumeNoGroupingAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }
}
