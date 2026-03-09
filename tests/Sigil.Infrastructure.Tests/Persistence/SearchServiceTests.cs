using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class SearchServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static SearchService Create(SigilDbContext ctx) => new(ctx);

    // ── BuildPrefixQuery (static, no DB) ─────────────────────────────────────

    [Fact]
    public void BuildPrefixQuery_SingleWord_ReturnsWordWithColonStar()
    {
        var result = SearchService.BuildPrefixQuery("error");

        result.Should().Be("error:*");
    }

    [Fact]
    public void BuildPrefixQuery_MultipleWords_JoinedWithAmpersand()
    {
        var result = SearchService.BuildPrefixQuery("null pointer exception");

        result.Should().Be("null:* & pointer:* & exception:*");
    }

    [Fact]
    public void BuildPrefixQuery_ExtraWhitespace_Trimmed()
    {
        var result = SearchService.BuildPrefixQuery("  foo   bar  ");

        result.Should().Be("foo:* & bar:*");
    }

    // ── SearchAsync — empty/whitespace ────────────────────────────────────────

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.SearchAsync("", projectId: null);

        result.Issues.Should().BeEmpty();
        result.Releases.Should().BeEmpty();
        result.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WhitespaceQuery_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.SearchAsync("   ", projectId: null);

        result.Issues.Should().BeEmpty();
        result.Releases.Should().BeEmpty();
        result.Tags.Should().BeEmpty();
    }

    // ── SearchAsync — Release search ──────────────────────────────────────────

    [Fact]
    public async Task Search_ReleaseByVersion_MatchesRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await TestHelper.CreateReleaseAsync(ctx, project.Id, "1.5.0-beta");
        var service = Create(ctx);

        var result = await service.SearchAsync("1.5.0", projectId: null);

        result.Releases.Should().ContainSingle(r => r.RawName == "1.5.0-beta");
    }

    [Fact]
    public async Task Search_ReleaseWithProjectId_OnlyReturnsProjectResults()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var prefix = Guid.NewGuid().ToString("N")[..8];
        // RawName must be unique across all projects — use distinct names
        await TestHelper.CreateReleaseAsync(ctx, project1.Id, $"{prefix}-p1-v1.0.0");
        await TestHelper.CreateReleaseAsync(ctx, project2.Id, $"{prefix}-p2-v1.0.0");
        var service = Create(ctx);

        var result = await service.SearchAsync(prefix, projectId: project1.Id);

        result.Releases.Should().AllSatisfy(r => r.ProjectId.Should().Be(project1.Id));
    }

    [Fact]
    public async Task Search_ReleasesExceedCap_ReturnsAtMostThree()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var prefix = Guid.NewGuid().ToString("N")[..8];
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateReleaseAsync(ctx, project.Id, $"{prefix}-{i}.0.0");
        var service = Create(ctx);

        var result = await service.SearchAsync(prefix, projectId: null);

        result.Releases.Should().HaveCountLessOrEqualTo(3);
    }

    [Fact]
    public async Task Search_NoMatchingRelease_ReturnsEmptyReleases()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await TestHelper.CreateReleaseAsync(ctx, project.Id, "9.9.9");
        var service = Create(ctx);

        var result = await service.SearchAsync("xyz-no-match-ever", projectId: null);

        result.Releases.Should().BeEmpty();
    }

    // ── SearchAsync — Issue search (tsvector) ─────────────────────────────────

    [Fact]
    public async Task Search_IssueByTitle_ReturnsMatch()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var word = $"srchword{Guid.NewGuid():N}"[..24];
        await TestHelper.CreateIssueAsync(ctx, project.Id, title: $"{word} unhandled exception");
        var service = Create(ctx);

        var result = await service.SearchAsync(word, projectId: null);

        result.Issues.Should().ContainSingle(i => i.ProjectId == project.Id);
    }

    [Fact]
    public async Task Search_IssueWithProjectFilter_ScopedToProject()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var word = $"scpword{Guid.NewGuid():N}"[..24];
        await TestHelper.CreateIssueAsync(ctx, project1.Id, title: $"{word} error in p1");
        await TestHelper.CreateIssueAsync(ctx, project2.Id, title: $"{word} error in p2");
        var service = Create(ctx);

        var result = await service.SearchAsync(word, projectId: project1.Id);

        result.Issues.Should().AllSatisfy(i => i.ProjectId.Should().Be(project1.Id));
    }

    [Fact]
    public async Task Search_IssueResults_CappedAtFive()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var word = $"capword{Guid.NewGuid():N}"[..24];
        for (int i = 0; i < 7; i++)
            await TestHelper.CreateIssueAsync(ctx, project.Id, title: $"{word} issue {i}");
        var service = Create(ctx);

        var result = await service.SearchAsync(word, projectId: null);

        result.Issues.Should().HaveCountLessOrEqualTo(5);
    }

    // ── SearchAsync — Tag search ──────────────────────────────────────────────

    [Fact]
    public async Task Search_TagByKey_ReturnsMatchingTag()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var tagKey = await TestHelper.CreateTagKeyAsync(ctx, $"searchkey{Guid.NewGuid():N}"[..24]);
        var tagValue = new Domain.Entities.TagValue { TagKeyId = tagKey.Id, Value = "myvalue" };
        ctx.TagValues.Add(tagValue);
        await ctx.SaveChangesAsync();
        ctx.IssueTags.Add(new Domain.Entities.IssueTag { IssueId = issue.Id, TagValueId = tagValue.Id });
        await ctx.SaveChangesAsync();
        var service = Create(ctx);

        var result = await service.SearchAsync(tagKey.Key[..12], projectId: null);

        result.Tags.Should().NotBeEmpty();
    }
}
