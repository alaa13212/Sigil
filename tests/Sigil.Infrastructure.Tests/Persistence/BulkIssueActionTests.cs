using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class BulkIssueActionTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static IIssueCache StubCache()
    {
        var cache = Substitute.For<IIssueCache>();
        cache.TryGet(Arg.Any<int>(), Arg.Any<string>(), out Arg.Any<Issue?>()).Returns(false);
        return cache;
    }

    private static IEventRanker StubRanker()
    {
        var ranker = Substitute.For<IEventRanker>();
        ranker.GetMostRelevantEvent(Arg.Any<IEnumerable<ParsedEvent>>())
            .Returns(ci => ci.Arg<IEnumerable<ParsedEvent>>().First());
        return ranker;
    }

    private static IEventFilterService StubFilterService(SigilDbContext ctx)
        => new EventFilterService(ctx, Substitute.For<IEventFilterCache>(), StubDateTime(), Substitute.For<IRuleEngine>());

    private IssueService Create(SigilDbContext ctx, IIssueActivityLogger? activityLogger = null, IEventFilterService? filterService = null)
        => new(ctx, StubRanker(), StubDateTime(), StubCache(),
               activityLogger ?? new IssueActivityService(ctx, StubDateTime()),
               filterService ?? StubFilterService(ctx));

    private static async Task<MergeSet> CreateMergeSetAsync(SigilDbContext ctx, int projectId, params int[] issueIds)
    {
        var mergeSet = new MergeSet
        {
            ProjectId = projectId,
            PrimaryIssueId = issueIds[0],
            CreatedAt = DateTime.UtcNow,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            OccurrenceCount = 1,
            Level = Severity.Error,
        };
        ctx.MergeSets.Add(mergeSet);
        await ctx.SaveChangesAsync();

        foreach (var issueId in issueIds)
        {
            var issue = await ctx.Issues.FirstAsync(i => i.Id == issueId);
            issue.MergeSetId = mergeSet.Id;
        }
        await ctx.SaveChangesAsync();

        return mergeSet;
    }

    [Fact]
    public async Task BulkResolve_SetsTheStatusAndTheResolveFieldsOnEveryIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var second = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var updated = await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([first.Id, second.Id], Status: IssueStatus.Resolved), user.Id);

        updated.Should().Be(2);
        await using var verify = Ctx();
        var issues = await verify.Issues.Where(i => i.Id == first.Id || i.Id == second.Id).ToListAsync();
        issues.Should().OnlyContain(i => i.Status == IssueStatus.Resolved);
        issues.Should().OnlyContain(i => i.ResolvedAt.HasValue);
        issues.Should().OnlyContain(i => i.ResolvedById == user.Id);
    }

    [Fact]
    public async Task BulkResolve_PropagatesToMergeSetSiblings()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var selected = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sibling = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await CreateMergeSetAsync(ctx, project.Id, selected.Id, sibling.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([selected.Id], Status: IssueStatus.Resolved), user.Id);

        await using var verify = Ctx();
        var propagated = await verify.Issues.FirstAsync(i => i.Id == sibling.Id);
        propagated.Status.Should().Be(IssueStatus.Resolved);
    }

    [Fact]
    public async Task BulkAssign_SetsTheAssigneeAndPropagatesToMergeSetSiblings()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var selected = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sibling = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await CreateMergeSetAsync(ctx, project.Id, selected.Id, sibling.Id);
        var assignee = await TestHelper.CreateUserAsync(ctx);
        var actor = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var updated = await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([selected.Id], AssignToUserId: assignee.Id), actor.Id);

        updated.Should().Be(1);
        await using var verify = Ctx();
        (await verify.Issues.FirstAsync(i => i.Id == selected.Id)).AssignedToId.Should().Be(assignee.Id);
        (await verify.Issues.FirstAsync(i => i.Id == sibling.Id)).AssignedToId.Should().Be(assignee.Id);
    }

    [Fact]
    public async Task BulkUnassign_ClearsTheAssignee()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var assignee = await TestHelper.CreateUserAsync(ctx);
        issue.AssignedToId = assignee.Id;
        await ctx.SaveChangesAsync();
        var actor = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([issue.Id], Unassign: true), actor.Id);

        await using var verify = Ctx();
        (await verify.Issues.FirstAsync(i => i.Id == issue.Id)).AssignedToId.Should().BeNull();
    }

    [Fact]
    public async Task BulkBookmark_CreatesUserIssueStateRowsForTheActingUser()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var second = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var updated = await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([first.Id, second.Id], Bookmarked: true), user.Id);

        updated.Should().Be(2);
        await using var verify = Ctx();
        var states = await verify.UserIssueStates.Where(s => s.UserId == user.Id).ToListAsync();
        states.Should().HaveCount(2);
        states.Should().OnlyContain(s => s.IsBookmarked);
        states.Should().OnlyContain(s => s.BookmarkedAt.HasValue);
    }

    [Fact]
    public async Task BulkBookmark_SetsTheStateInsteadOfTogglingIt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = user.Id,
            IssueId = issue.Id,
            IsBookmarked = true,
            BookmarkedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        var service = Create(ctx);

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([issue.Id], Bookmarked: true), user.Id);

        await using var verify = Ctx();
        var state = await verify.UserIssueStates.FirstAsync(s => s.UserId == user.Id && s.IssueId == issue.Id);
        state.IsBookmarked.Should().BeTrue();
    }

    [Fact]
    public async Task BulkIgnoreWithFutureEvents_CreatesOneEventFilterPerIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var second = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([first.Id, second.Id], Status: IssueStatus.Ignored, IgnoreFutureEvents: true), user.Id);

        await using var verify = Ctx();
        var issues = await verify.Issues.Where(i => i.Id == first.Id || i.Id == second.Id).ToListAsync();
        issues.Should().OnlyContain(i => i.Status == IssueStatus.Ignored);
        issues.Should().OnlyContain(i => i.IgnoreFilterId.HasValue);
        issues.Select(i => i.IgnoreFilterId!.Value).Distinct().Should().HaveCount(2);

        var filters = await verify.EventFilters.Where(f => f.ProjectId == project.Id).ToListAsync();
        filters.Should().HaveCount(2);
        filters.Should().OnlyContain(f => f.Field == "fingerprint");
        filters.Should().OnlyContain(f => f.Value == first.Fingerprint || f.Value == second.Fingerprint);
    }

    [Fact]
    public async Task BulkWithAnIssueFromAnotherProject_IsRejectedInFull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var other = await TestHelper.CreateProjectAsync(ctx);
        var mine = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var theirs = await TestHelper.CreateIssueAsync(ctx, other.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var act = () => service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([mine.Id, theirs.Id], Status: IssueStatus.Resolved), user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var verify = Ctx();
        (await verify.Issues.FirstAsync(i => i.Id == mine.Id)).Status.Should().NotBe(IssueStatus.Resolved);
    }

    [Fact]
    public async Task BulkWithAnEmptyBatch_IsRejected()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var act = () => service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([], Status: IssueStatus.Resolved), user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BulkWithTwoActions_IsRejected()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var act = () => service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([issue.Id], Status: IssueStatus.Resolved, Bookmarked: true), user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Bulk_WritesAnActivityRowForEveryIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var second = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([first.Id, second.Id], Status: IssueStatus.Resolved), user.Id);

        await using var verify = Ctx();
        var activities = await verify.IssueActivities.Where(a => a.UserId == user.Id).ToListAsync();
        activities.Should().HaveCount(2);
        activities.Should().OnlyContain(a => a.Action == IssueActivityAction.Resolved);
    }

    [Fact]
    public async Task Bulk_InvalidatesTheIssueCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var cache = StubCache();
        var service = new IssueService(ctx, StubRanker(), StubDateTime(), cache,
            new IssueActivityService(ctx, StubDateTime()), StubFilterService(ctx));

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([issue.Id], Status: IssueStatus.Resolved), user.Id);

        cache.Received().InvalidateAll();
    }

    [Fact]
    public async Task BulkUnresolve_ClearsTheResolveFields()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var resolver = await TestHelper.CreateUserAsync(ctx);
        issue.Status = IssueStatus.Resolved;
        issue.ResolvedAt = DateTime.UtcNow;
        issue.ResolvedById = resolver.Id;
        await ctx.SaveChangesAsync();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.BulkUpdateIssuesAsync(project.Id,
            new BulkIssueActionRequest([issue.Id], Status: IssueStatus.Open), user.Id);

        await using var verify = Ctx();
        var reopened = await verify.Issues.FirstAsync(i => i.Id == issue.Id);
        reopened.Status.Should().Be(IssueStatus.Open);
        reopened.ResolvedAt.Should().BeNull();
        reopened.ResolvedById.Should().BeNull();
    }
}
