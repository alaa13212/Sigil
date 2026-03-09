using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class NoAlertsConfiguredAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_NoEvents_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var analyzer = new NoAlertsConfiguredAnalyzer(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_EventsButNoAlerts_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var analyzer = new NoAlertsConfiguredAnalyzer(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("no-alerts-configured");
    }

    [Fact]
    public async Task Analyze_HasEnabledAlertRule_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id, enabled: true);
        var analyzer = new NoAlertsConfiguredAnalyzer(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_HasDisabledAlertRule_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id, enabled: false);
        var analyzer = new NoAlertsConfiguredAnalyzer(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().NotBeNull();
    }
}
