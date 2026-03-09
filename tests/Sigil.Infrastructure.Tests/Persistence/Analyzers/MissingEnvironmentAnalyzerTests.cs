using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class MissingEnvironmentAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_NoEvents_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var analyzer = new MissingEnvironmentAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_EventsWithEnvironmentTag_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var tagKey = await TestHelper.CreateTagKeyAsync(ctx, "environment");
        var tagValue = new TagValue { TagKeyId = tagKey.Id, Value = "production" };
        ctx.TagValues.Add(tagValue);
        await ctx.SaveChangesAsync();
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        ctx.EventTags.Add(new EventTag { EventId = evt.Id, TagValueId = tagValue.Id });
        await ctx.SaveChangesAsync();
        var analyzer = new MissingEnvironmentAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_EventsWithoutEnvironmentTag_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var analyzer = new MissingEnvironmentAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("missing-environment");
        result.Title.Should().Contain("environment");
    }

    [Fact]
    public void Properties_AreCorrect()
    {
        using var ctx = Ctx();
        var analyzer = new MissingEnvironmentAnalyzer(ctx);

        analyzer.AnalyzerId.Should().Be("missing-environment");
        analyzer.IsRepeatable.Should().BeFalse();
    }
}
