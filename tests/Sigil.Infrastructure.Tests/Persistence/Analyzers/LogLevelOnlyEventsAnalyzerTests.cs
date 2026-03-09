using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class LogLevelOnlyEventsAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_TooFewEvents_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 5, Severity.Warning);
        var analyzer = new LogLevelOnlyEventsAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_OnlyLowLevelEvents_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 25, Severity.Warning);
        var analyzer = new LogLevelOnlyEventsAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("log-level-only-events");
    }

    [Fact]
    public async Task Analyze_HasErrorEvents_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 20, Severity.Warning);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 5, Severity.Error);
        var analyzer = new LogLevelOnlyEventsAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }
}
