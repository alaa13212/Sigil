using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class AnonymousUsersEverywhereAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_TooFewEventsWithUser_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateEventUserAsync(ctx, "anonymous");
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 10, userId: user.UniqueIdentifier);
        var analyzer = new AnonymousUsersEverywhereAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_MostUsersAnonymous_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var anonUser = await TestHelper.CreateEventUserAsync(ctx, "anonymous");
        // 25 events with anonymous user
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 25, userId: anonUser.UniqueIdentifier);
        var analyzer = new AnonymousUsersEverywhereAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("anonymous-users-everywhere");
    }

    [Fact]
    public async Task Analyze_MostUsersIdentified_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var realUser = await TestHelper.CreateEventUserAsync(ctx, "user123");
        var anonUser = await TestHelper.CreateEventUserAsync(ctx, "anonymous");
        // 20 events with real user, 5 anonymous (< 80%)
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 20, userId: realUser.UniqueIdentifier);
        await AnalyzerTestHelper.CreateEventsAsync(ctx, project.Id, issue.Id, 5, userId: anonUser.UniqueIdentifier);
        var analyzer = new AnonymousUsersEverywhereAnalyzer(ctx, AnalyzerTestHelper.StubDateTime());

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }
}
