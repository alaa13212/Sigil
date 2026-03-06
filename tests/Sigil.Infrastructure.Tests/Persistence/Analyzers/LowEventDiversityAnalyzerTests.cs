using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class LowEventDiversityAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_OnlyOneIssue_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await TestHelper.CreateIssueAsync(ctx, project.Id);
        var analyzer = new LowEventDiversityAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_NormalDiversity_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // 10 events total, 2 issues => 5 events/issue (below 5000)
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue1.Id, 5);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue2.Id, 5);
        var analyzer = new LowEventDiversityAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_HighEventsPerIssue_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // 2 issues, 10002 events => 5001 events/issue (above 5000 threshold)
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue1.Id, 5001);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue2.Id, 5001);
        var analyzer = new LowEventDiversityAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("low-event-diversity");
    }

    [Fact]
    public void Properties_AreCorrect()
    {
        using var ctx = Ctx();
        var analyzer = new LowEventDiversityAnalyzer(ctx);

        analyzer.AnalyzerId.Should().Be("low-event-diversity");
        analyzer.IsRepeatable.Should().BeFalse();
    }
}
