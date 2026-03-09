using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class BadgeServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);
    private static BadgeService Create(SigilDbContext ctx) => new(ctx);

    private async Task SetPageViewAsync(SigilDbContext ctx, Guid userId, int projectId, PageType pageType, DateTime viewedAt)
    {
        // Upsert via direct entity add (each test uses isolated project/user combos)
        ctx.UserPageViews.Add(new UserPageView
        {
            UserId = userId,
            ProjectId = projectId,
            PageType = pageType,
            LastViewedAt = viewedAt,
        });
        await ctx.SaveChangesAsync();
    }

    // ── GetAllBadgeCounts ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetBadgeCounts_NoPageViewRecord_ReturnsAllAsNew()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"badge-v1.0-{Guid.NewGuid():N}");

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetAllBadgeCountsAsync(user.Id);

        result.Should().ContainKey(project.Id);
        result[project.Id].UnseenIssues.Should().BeGreaterThan(0);
        result[project.Id].UnseenReleases.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBadgeCounts_IssuesModifiedAfterLastView_ReturnsCount()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var oldView = DateTime.UtcNow.AddHours(-1);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.LastChangedAt = DateTime.UtcNow; // newer than the view
        await ctx.SaveChangesAsync();
        await SetPageViewAsync(ctx, user.Id, project.Id, PageType.Issues, oldView);

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetAllBadgeCountsAsync(user.Id);

        result.Should().ContainKey(project.Id);
        result[project.Id].UnseenIssues.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBadgeCounts_IssuesModifiedBeforeLastView_ZeroCount()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.LastChangedAt = DateTime.UtcNow.AddHours(-2);
        await ctx.SaveChangesAsync();
        await SetPageViewAsync(ctx, user.Id, project.Id, PageType.Issues, DateTime.UtcNow.AddHours(-1));

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetAllBadgeCountsAsync(user.Id);

        // Either no entry for this project or issue count is 0
        var issueCount = result.TryGetValue(project.Id, out var counts) ? counts.UnseenIssues : 0;
        issueCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBadgeCounts_ReleasesAfterLastView_ReturnsCount()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var oldView = DateTime.UtcNow.AddHours(-2);
        await SetPageViewAsync(ctx, user.Id, project.Id, PageType.Releases, oldView);
        await TestHelper.CreateReleaseAsync(ctx, project.Id, $"badge-v2.0-{Guid.NewGuid():N}", firstSeen: DateTime.UtcNow.AddHours(-1));

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetAllBadgeCountsAsync(user.Id);

        result.Should().ContainKey(project.Id);
        result[project.Id].UnseenReleases.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBadgeCounts_MultipleProjects_CountedSeparately()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        await TestHelper.CreateIssueAsync(ctx, project1.Id);
        await TestHelper.CreateIssueAsync(ctx, project2.Id);
        await TestHelper.CreateIssueAsync(ctx, project2.Id);

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetAllBadgeCountsAsync(user.Id);

        result.Should().ContainKey(project1.Id);
        result.Should().ContainKey(project2.Id);
        result[project1.Id].UnseenIssues.Should().Be(1);
        result[project2.Id].UnseenIssues.Should().Be(2);
    }

    [Fact]
    public async Task GetBadgeCounts_ProjectWithNoNewItems_NotInResult()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        // Create an issue and mark it as already viewed
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.LastChangedAt = DateTime.UtcNow.AddHours(-2);
        await ctx.SaveChangesAsync();
        // Record a page view AFTER the issue changed (so issue is "seen")
        await SetPageViewAsync(ctx, user.Id, project.Id, PageType.Issues, DateTime.UtcNow.AddHours(-1));

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetAllBadgeCountsAsync(user.Id);

        // This project should have 0 unseen issues (they were viewed after last change)
        var issueCount = result.TryGetValue(project.Id, out var counts) ? counts.UnseenIssues : 0;
        issueCount.Should().Be(0);
    }
}
