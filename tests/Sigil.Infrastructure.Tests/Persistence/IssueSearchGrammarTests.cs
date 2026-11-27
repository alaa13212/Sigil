using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class IssueSearchGrammarTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IssueService Create(SigilDbContext ctx) => new(
        ctx,
        Substitute.For<IEventRanker>(),
        Substitute.For<IDateTime>(),
        Substitute.For<IIssueCache>(),
        Substitute.For<IIssueActivityLogger>(),
        Substitute.For<IEventFilterService>());

    private static string UniqueRelease() => $"app@{Guid.NewGuid():N}";

    private static async Task TagIssueAsync(SigilDbContext ctx, int issueId, string key, string value)
    {
        var tagKey = await ctx.TagKeys.FirstOrDefaultAsync(t => t.Key == key) ??
                     await TestHelper.CreateTagKeyAsync(ctx, key);
        var tagValue = await ctx.TagValues.FirstOrDefaultAsync(v => v.TagKeyId == tagKey.Id && v.Value == value);
        if (tagValue is null)
        {
            tagValue = new TagValue { TagKeyId = tagKey.Id, Value = value };
            ctx.TagValues.Add(tagValue);
            await ctx.SaveChangesAsync();
        }

        ctx.IssueTags.Add(new IssueTag { IssueId = issueId, TagValueId = tagValue.Id });
        await ctx.SaveChangesAsync();
    }

    private static async Task<DateTime> SetLastChangedAsync(SigilDbContext ctx, int issueId, DateTime value)
    {
        var issue = await ctx.Issues.FindAsync(issueId);
        issue!.LastChangedAt = value;
        await ctx.SaveChangesAsync();
        return value;
    }

    // ── release: ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_ReleaseToken_OnlyReturnsIssuesSeenInThatRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, releaseName);
        var otherRelease = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());

        var inRelease = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var inOtherRelease = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var neverSeen = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, inRelease.Id, releaseId: release.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, inOtherRelease.Id, releaseId: otherRelease.Id);

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = $"release:{releaseName}" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([inRelease.Id]);
    }

    [Fact]
    public async Task GetSummaries_ReleaseTokenWithAnUnknownName_ReturnsNothing()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = $"release:{UniqueRelease()}" });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Items.Should().NotContain(i => i.Id == issue.Id);
    }

    [Fact]
    public async Task GetSummaries_SeveralReleaseTokens_RequireEveryRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());
        var second = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());

        var both = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var onlyFirst = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, both.Id, releaseId: first.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, both.Id, releaseId: second.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, onlyFirst.Id, releaseId: first.Id);

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = $"release:{first.RawName} release:{second.RawName}" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([both.Id]);
    }

    [Fact]
    public async Task GetSummaries_ReleaseToken_AppliesTogetherWithATagFilter()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());
        var env = UniqueRelease();

        var matching = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var wrongTag = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var wrongRelease = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, matching.Id, releaseId: release.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, wrongTag.Id, releaseId: release.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, wrongRelease.Id);
        await TagIssueAsync(ctx, matching.Id, "env", env);
        await TagIssueAsync(ctx, wrongRelease.Id, "env", env);
        await TagIssueAsync(ctx, wrongTag.Id, "env", "other");

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = $"env:{env} release:{release.RawName}" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([matching.Id]);
    }

    // ── is:assigned / is:unassigned ──────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_AssignedToken_OnlyReturnsAssignedIssues()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var assigned = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var unassigned = await TestHelper.CreateIssueAsync(ctx, project.Id);
        assigned.AssignedToId = user.Id;
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "is:assigned" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([assigned.Id]);
        result.Items.Should().NotContain(i => i.Id == unassigned.Id);
    }

    [Fact]
    public async Task GetSummaries_UnassignedToken_OnlyReturnsIssuesWithoutAnAssignee()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var assigned = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var unassigned = await TestHelper.CreateIssueAsync(ctx, project.Id);
        assigned.AssignedToId = user.Id;
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "is:unassigned" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([unassigned.Id]);
        result.Items.Should().NotContain(i => i.Id == assigned.Id);
    }

    [Fact]
    public async Task GetSummaries_AssignmentToken_AppliesTogetherWithAReleaseToken()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());
        var unassigned = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var assigned = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, unassigned.Id, releaseId: release.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, assigned.Id, releaseId: release.Id);
        assigned.AssignedToId = user.Id;
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = $"is:unassigned release:{release.RawName}" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([unassigned.Id]);
    }

    // ── is:bookmarked ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_BookmarkedToken_OnlyReturnsTheViewersBookmarks()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var viewer = await TestHelper.CreateUserAsync(ctx);
        var other = await TestHelper.CreateUserAsync(ctx);
        var mine = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var notMine = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var theirs = await TestHelper.CreateIssueAsync(ctx, project.Id);
        ctx.UserIssueStates.Add(new UserIssueState { UserId = viewer.Id, IssueId = mine.Id, IsBookmarked = true });
        ctx.UserIssueStates.Add(new UserIssueState { UserId = other.Id, IssueId = theirs.Id, IsBookmarked = true });
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id, new IssueQueryParams
        {
            Search = "is:bookmarked",
            BookmarkedByUserId = viewer.Id
        });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([mine.Id]);
        result.Items.Should().NotContain(i => i.Id == notMine.Id || i.Id == theirs.Id);
    }

    [Fact]
    public async Task GetSummaries_BookmarkedTokenWithoutAViewer_ReturnsNothing()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var viewer = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        ctx.UserIssueStates.Add(new UserIssueState { UserId = viewer.Id, IssueId = issue.Id, IsBookmarked = true });
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "is:bookmarked" });

        result.Items.Should().BeEmpty();
    }

    // ── is:unviewed ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_UnviewedToken_ExcludesIssuesTheViewerHasSeenSinceTheyChanged()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var viewer = await TestHelper.CreateUserAsync(ctx);
        var changed = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var neverViewed = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var viewedBeforeChange = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var viewedAfterChange = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await SetLastChangedAsync(ctx, neverViewed.Id, changed);
        await SetLastChangedAsync(ctx, viewedBeforeChange.Id, changed);
        await SetLastChangedAsync(ctx, viewedAfterChange.Id, changed);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = viewer.Id, IssueId = viewedBeforeChange.Id, LastViewedAt = changed.AddDays(-1)
        });
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = viewer.Id, IssueId = viewedAfterChange.Id, LastViewedAt = changed.AddDays(1)
        });
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id, new IssueQueryParams
        {
            Search = "is:unviewed",
            ViewerUserId = viewer.Id
        });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([neverViewed.Id, viewedBeforeChange.Id]);
        result.Items.Should().NotContain(i => i.Id == viewedAfterChange.Id);
    }

    [Fact]
    public async Task GetSummaries_UnviewedToken_MatchesTheUnviewedFlagOnTheSummary()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var viewer = await TestHelper.CreateUserAsync(ctx);
        var changed = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var unviewed = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var viewed = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await SetLastChangedAsync(ctx, unviewed.Id, changed);
        await SetLastChangedAsync(ctx, viewed.Id, changed);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = viewer.Id, IssueId = viewed.Id, LastViewedAt = changed.AddDays(1)
        });
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id, new IssueQueryParams
        {
            Search = "is:unviewed",
            ViewerUserId = viewer.Id
        });

        result.Items.Should().ContainSingle().Which.IsUnviewed.Should().BeTrue();
    }

    [Fact]
    public async Task GetSummaries_UnviewedTokenWithoutAViewer_ReturnsNothing()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await TestHelper.CreateIssueAsync(ctx, project.Id);

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "is:unviewed" });

        result.Items.Should().BeEmpty();
    }

    // ── tag filter combination ───────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_SeveralTagFilters_RequireEveryOneOfThem()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var both = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var onlyFirst = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var onlySecond = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TagIssueAsync(ctx, both.Id, "env", "prod");
        await TagIssueAsync(ctx, both.Id, "region", "eu");
        await TagIssueAsync(ctx, onlyFirst.Id, "env", "prod");
        await TagIssueAsync(ctx, onlySecond.Id, "region", "eu");

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "env:prod region:eu" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([both.Id]);
    }

    [Fact]
    public async Task GetSummaries_RepeatedTagFilter_StillMatchesTheIssuesThatHaveIt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var tagged = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var untagged = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TagIssueAsync(ctx, tagged.Id, "env", "prod");

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "env:prod env:prod" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([tagged.Id]);
        result.Items.Should().NotContain(i => i.Id == untagged.Id);
    }

    // ── regression guards ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_NoSearch_FiltersNothing()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var second = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());
        await TestHelper.CreateEventAsync(ctx, project.Id, first.Id, releaseId: release.Id);

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id, new IssueQueryParams());

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetSummaries_FreeTextOnly_IsUnaffectedByTheReservedTokens()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id, "Test Issue");
        await TagIssueAsync(ctx, issue.Id, "env", "prod");

        var result = await Create(ctx).GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Search = "env:prod" });

        result.Items.Select(i => i.Id).Should().BeEquivalentTo([issue.Id]);
    }
}
