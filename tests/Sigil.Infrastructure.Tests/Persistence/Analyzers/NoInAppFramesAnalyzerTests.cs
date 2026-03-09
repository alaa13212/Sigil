using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class NoInAppFramesAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_NoStackFrames_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var analyzer = new NoInAppFramesAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_OnlyFrameworkFrames_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        await TestHelper.CreateStackFrameAsync(ctx, evt.Id, "System.String.Concat", inApp: false);
        var analyzer = new NoInAppFramesAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("no-in-app-frames");
    }

    [Fact]
    public async Task Analyze_HasInAppFrames_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        await TestHelper.CreateStackFrameAsync(ctx, evt.Id, "MyApp.Handler", inApp: true);
        var analyzer = new NoInAppFramesAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }
}
