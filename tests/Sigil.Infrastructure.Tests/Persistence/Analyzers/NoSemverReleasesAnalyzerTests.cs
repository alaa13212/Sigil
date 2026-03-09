using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class NoSemverReleasesAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_TooFewReleases_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);
        var tag = Guid.NewGuid().ToString("N")[..8];
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-1-{tag}");
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-2-{tag}");
        var analyzer = new NoSemverReleasesAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_NoSemverReleases_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);
        var tag = Guid.NewGuid().ToString("N")[..8];
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-1-{tag}");
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-2-{tag}");
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-3-{tag}");
        var analyzer = new NoSemverReleasesAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("no-semver-releases");
    }

    [Fact]
    public async Task Analyze_HasSemverRelease_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var platformInfo = TestHelper.GetPlatformInfo(project.Platform);
        var tag = Guid.NewGuid().ToString("N")[..8];
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-1-{tag}");
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"build-2-{tag}");
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"myapp-{tag}@1.2.3", semanticVersion: "1.2.3");
        var analyzer = new NoSemverReleasesAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project, platformInfo);

        result.Should().BeNull();
    }
}
