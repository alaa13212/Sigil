using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class IssueServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IIssueCache MissCache()
    {
        var cache = Substitute.For<IIssueCache>();
        cache.TryGet(Arg.Any<int>(), Arg.Any<string>(), out Arg.Any<Issue?>()).Returns(false);
        return cache;
    }

    private static IDateTime StubDateTime(DateTime? utcNow = null)
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(utcNow ?? new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static IEventRanker StubRanker()
    {
        var ranker = Substitute.For<IEventRanker>();
        ranker.GetMostRelevantEvent(Arg.Any<IEnumerable<ParsedEvent>>())
            .Returns(ci => ci.Arg<IEnumerable<ParsedEvent>>().First());
        return ranker;
    }

    private static IIssueActivityLogger StubActivityLogger()
    {
        var logger = Substitute.For<IIssueActivityLogger>();
        logger.LogActivityAsync(Arg.Any<int>(), Arg.Any<IssueActivityAction>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>())
            .Returns(new IssueActivity { Action = IssueActivityAction.Created, Timestamp = DateTime.UtcNow });
        return logger;
    }

    private static IEventFilterService StubFilterService()
        => Substitute.For<IEventFilterService>();

    private IssueService Create(SigilDbContext ctx,
        IIssueCache? cache = null,
        IDateTime? dateTime = null,
        IIssueActivityLogger? activityLogger = null,
        IEventFilterService? filterService = null)
        => new(ctx, StubRanker(), dateTime ?? StubDateTime(), cache ?? MissCache(),
               activityLogger ?? StubActivityLogger(), filterService ?? StubFilterService());

    private static ParsedEvent MakeParsedEvent(string fingerprint, string? message = null) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        Timestamp = DateTime.UtcNow,
        ReceivedAt = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        NormalizedMessage = message ?? "Test error",
        Fingerprint = fingerprint,
    };

    // ── BulkGetOrCreateIssuesAsync ────────────────────────────────────────────

    [Fact]
    public async Task BulkGetOrCreate_NewFingerprint_CreatesIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);
        var fingerprint = Guid.NewGuid().ToString("N")[..32];
        var events = new[] { MakeParsedEvent(fingerprint) }.GroupBy(e => e.Fingerprint!);

        var result = await service.BulkGetOrCreateIssuesAsync(project, events);

        result.Should().HaveCount(1);
        result[0].ProjectId.Should().Be(project.Id);
        result[0].Fingerprint.Should().Be(fingerprint);
        result[0].Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkGetOrCreate_ExistingFingerprint_ReturnsExistingIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var existing = await TestHelper.CreateIssueAsync(ctx, project.Id);

        await using var ctx2 = Ctx();
        var service = Create(ctx2);
        var events = new[] { MakeParsedEvent(existing.Fingerprint) }.GroupBy(e => e.Fingerprint!);

        var result = await service.BulkGetOrCreateIssuesAsync(project, events);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task BulkGetOrCreate_NewIssue_LogsCreatedActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var activityLogger = StubActivityLogger();
        var service = Create(ctx, activityLogger: activityLogger);
        var fingerprint = Guid.NewGuid().ToString("N")[..32];
        var events = new[] { MakeParsedEvent(fingerprint) }.GroupBy(e => e.Fingerprint!);

        await service.BulkGetOrCreateIssuesAsync(project, events);

        // Activity is embedded in issue.Activities, not logged separately via activityLogger
        await using var verifyCtx = Ctx();
        var issue = verifyCtx.Issues.FirstOrDefault(i => i.Fingerprint == fingerprint);
        issue.Should().NotBeNull();
        var activity = verifyCtx.IssueActivities.FirstOrDefault(a => a.IssueId == issue!.Id && a.Action == IssueActivityAction.Created);
        activity.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkGetOrCreate_CacheMiss_SetsIssueInCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var cache = MissCache();
        var service = Create(ctx, cache: cache);
        var fingerprint = Guid.NewGuid().ToString("N")[..32];
        var events = new[] { MakeParsedEvent(fingerprint) }.GroupBy(e => e.Fingerprint!);

        await service.BulkGetOrCreateIssuesAsync(project, events);

        cache.Received().Set(Arg.Is<Issue>(i => i.Fingerprint == fingerprint));
    }

    // ── UpdateIssueStatusAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_Resolved_PersistsStatus()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        await service.UpdateIssueStatusAsync(issue.Id, IssueStatus.Resolved);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.Status.Should().Be(IssueStatus.Resolved);
    }

    [Fact]
    public async Task UpdateStatus_Ignored_PersistsStatus()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        await service.UpdateIssueStatusAsync(issue.Id, IssueStatus.Ignored);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.Status.Should().Be(IssueStatus.Ignored);
    }

    [Fact]
    public async Task UpdateStatus_Resolved_LogsActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx); // real user needed for ResolvedById FK
        var activityLogger = StubActivityLogger();

        await using var ctx2 = Ctx();
        var service = Create(ctx2, activityLogger: activityLogger);

        await service.UpdateIssueStatusAsync(issue.Id, IssueStatus.Resolved, user.Id);

        await activityLogger.Received(1).LogActivityAsync(issue.Id, IssueActivityAction.Resolved, user.Id, Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task UpdateStatus_Resolved_SetsResolvedAtAndCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var resolvedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var cache = MissCache();
        var service = Create(ctx, cache: cache, dateTime: StubDateTime(resolvedAt));

        await service.UpdateIssueStatusAsync(issue.Id, IssueStatus.Resolved);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.ResolvedAt.Should().Be(resolvedAt);
        cache.Received().InvalidateAll();
    }

    // ── UpdateIssuePriorityAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdatePriority_SetsPriority()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        await service.UpdateIssuePriorityAsync(issue.Id, Priority.High);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public async Task UpdatePriority_Changed_LogsPriorityChangedActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var activityLogger = StubActivityLogger();
        var service = Create(ctx, activityLogger: activityLogger);

        // Issue starts at Medium priority (from TestHelper); change to High
        await service.UpdateIssuePriorityAsync(issue.Id, Priority.High);

        await activityLogger.Received(1).LogActivityAsync(
            issue.Id, IssueActivityAction.PriorityChanged,
            Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task UpdatePriority_SamePriority_DoesNotLogActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id); // starts at Medium
        var activityLogger = StubActivityLogger();
        var service = Create(ctx, activityLogger: activityLogger);

        await service.UpdateIssuePriorityAsync(issue.Id, Priority.Medium);

        await activityLogger.DidNotReceive().LogActivityAsync(
            Arg.Any<int>(), IssueActivityAction.PriorityChanged,
            Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    // ── AssignIssueAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Assign_SetAssignee()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.AssignIssueAsync(issue.Id, user.Id);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.AssignedToId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Assign_NullUserId_ClearsAssignment()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var tracked = await ctx.Issues.FindAsync(issue.Id);
        tracked!.AssignedToId = user.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = Create(ctx2);

        await service.AssignIssueAsync(issue.Id, assignToUserId: null);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.AssignedToId.Should().BeNull();
    }

    // ── DeleteIssueAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingIssue_RemovesFromDb()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        var result = await service.DeleteIssueAsync(issue.Id);

        result.Should().BeTrue();
        await using var verifyCtx = Ctx();
        verifyCtx.Issues.Any(i => i.Id == issue.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.DeleteIssueAsync(int.MaxValue);

        result.Should().BeFalse();
    }

    // ── GetIssueSummariesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaries_ReturnsPagedResponse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        for (int i = 0; i < 3; i++)
            await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        var result = await service.GetIssueSummariesAsync(project.Id, new IssueQueryParams());

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSummaries_FilterByStatus_OnlyMatchingReturned()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var openIssue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var resolvedIssue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var toResolve = await ctx.Issues.FindAsync(resolvedIssue.Id);
        toResolve!.Status = IssueStatus.Resolved;
        await ctx.SaveChangesAsync();
        var service = Create(ctx);

        var result = await service.GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { Status = IssueStatus.Open });

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(openIssue.Id);
    }

    [Fact]
    public async Task GetSummaries_SortedByLastSeenDescending()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = DateTime.UtcNow;
        var older = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var newer = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var olderTracked = await ctx.Issues.FindAsync(older.Id);
        var newerTracked = await ctx.Issues.FindAsync(newer.Id);
        olderTracked!.LastSeen = now.AddHours(-2);
        newerTracked!.LastSeen = now;
        await ctx.SaveChangesAsync();
        var service = Create(ctx);

        var result = await service.GetIssueSummariesAsync(project.Id,
            new IssueQueryParams { SortBy = IssueSortBy.LastSeen, SortDescending = true });

        result.Items[0].Id.Should().Be(newer.Id);
        result.Items[1].Id.Should().Be(older.Id);
    }

    // ── GetHistogramAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistogram_NoEventBuckets_ReturnsAllZeros()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        var result = await service.GetHistogramAsync(issue.Id, days: 7);

        result.Should().HaveCount(7);
        result.Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public async Task GetHistogram_WithBuckets_ReturnsDailyCount()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var today = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc); // matches StubDateTime default
        ctx.EventBuckets.Add(new EventBucket
        {
            IssueId = issue.Id,
            BucketStart = today,
            Count = 5,
        });
        await ctx.SaveChangesAsync();
        var service = Create(ctx);

        var result = await service.GetHistogramAsync(issue.Id, days: 7);

        result.Should().HaveCount(7);
        result.Last().Should().Be(5); // today is the last bucket
    }

    // ── GetIssueByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_NotFound_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.GetIssueByIdAsync(int.MaxValue);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_Found_ReturnsIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        await using var ctx2 = Ctx();
        var service = Create(ctx2);

        var result = await service.GetIssueByIdAsync(issue.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(issue.Id);
    }

    // ── GetSimilarIssuesAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSimilarIssues_NoMatches_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        var result = await service.GetSimilarIssuesAsync(issue.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSimilarIssues_SameExceptionTypeAndTitle_ReturnsSimilar()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        // Create two issues with the same ExceptionType and Title
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id);

        // Make them share the same ExceptionType and Title
        var t1 = await ctx.Issues.FindAsync(issue1.Id);
        var t2 = await ctx.Issues.FindAsync(issue2.Id);
        var sharedType = $"SimilarEx_{Guid.NewGuid():N}";
        var sharedTitle = $"Title_{Guid.NewGuid():N}";
        t1!.ExceptionType = sharedType;
        t1.Title = sharedTitle;
        t2!.ExceptionType = sharedType;
        t2.Title = sharedTitle;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = Create(ctx2);

        var result = await service.GetSimilarIssuesAsync(issue1.Id);

        result.Should().ContainSingle(i => i.Id == issue2.Id);
    }

    // ── RecordPageViewAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RecordPageView_NewView_CreatesRecord()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        await service.RecordPageViewAsync(user.Id, project.Id, PageType.Issues);

        await using var verifyCtx = Ctx();
        var view = verifyCtx.UserPageViews.FirstOrDefault(v =>
            v.UserId == user.Id && v.ProjectId == project.Id && v.PageType == PageType.Issues);
        view.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordPageView_ExistingView_UpdatesLastViewedAt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var oldTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.UserPageViews.Add(new UserPageView
        {
            UserId = user.Id,
            ProjectId = project.Id,
            PageType = PageType.Issues,
            LastViewedAt = oldTime,
        });
        await ctx.SaveChangesAsync();

        var newTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var ctx2 = Ctx();
        var service = Create(ctx2, dateTime: StubDateTime(newTime));

        await service.RecordPageViewAsync(user.Id, project.Id, PageType.Issues);

        await using var verifyCtx = Ctx();
        var view = verifyCtx.UserPageViews.First(v =>
            v.UserId == user.Id && v.ProjectId == project.Id && v.PageType == PageType.Issues);
        view.LastViewedAt.Should().Be(newTime);
    }

    // ── GetBulkHistogramsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetBulkHistograms_EmptyInput_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.GetBulkHistogramsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBulkHistograms_MultipleIssues_ReturnsAllKeys()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var today = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc); // matches StubDateTime default
        ctx.EventBuckets.AddRange(
            new EventBucket { IssueId = issue1.Id, BucketStart = today, Count = 3 },
            new EventBucket { IssueId = issue2.Id, BucketStart = today, Count = 7 }
        );
        await ctx.SaveChangesAsync();
        var service = Create(ctx);

        var result = await service.GetBulkHistogramsAsync([issue1.Id, issue2.Id], days: 7);

        result.Should().ContainKey(issue1.Id);
        result.Should().ContainKey(issue2.Id);
        result[issue1.Id].Last().Should().Be(3);
        result[issue2.Id].Last().Should().Be(7);
    }

    // ── UpdateStatus / UpdatePriority — missing issue throws ─────────────────

    [Fact]
    public async Task UpdateStatus_IssueMissing_Throws()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var act = () => service.UpdateIssueStatusAsync(int.MaxValue, IssueStatus.Resolved);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePriority_IssueMissing_Throws()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var act = () => service.UpdateIssuePriorityAsync(int.MaxValue, Priority.High);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── UpdateStatus ignoreFutureEvents ──────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_IgnoreFutureEvents_CreatesFilter()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        // Pre-create a real EventFilter row so the FK constraint is satisfied when the service saves
        var realFilter = new Domain.Entities.EventFilter
        {
            ProjectId = project.Id,
            Field = "fingerprint",
            Operator = Domain.Enums.FilterOperator.Equals,
            Value = issue.Fingerprint,
            Action = Domain.Enums.FilterAction.Reject,
        };
        ctx.EventFilters.Add(realFilter);
        await ctx.SaveChangesAsync();

        var filterService = StubFilterService();
        filterService.CreateFilterAsync(Arg.Any<int>(), Arg.Any<Application.Models.Filters.CreateFilterRequest>())
            .Returns(new Application.Models.Filters.EventFilterResponse(
                Id: realFilter.Id, ProjectId: project.Id, Field: "fingerprint",
                Operator: Domain.Enums.FilterOperator.Equals, Value: issue.Fingerprint,
                Action: Domain.Enums.FilterAction.Reject, Enabled: true,
                Priority: 0, Description: null, CreatedAt: DateTime.UtcNow));

        await using var ctx2 = Ctx();
        var service = Create(ctx2, filterService: filterService);

        await service.UpdateIssueStatusAsync(issue.Id, IssueStatus.Ignored, ignoreFutureEvents: true);

        await filterService.Received(1).CreateFilterAsync(
            project.Id,
            Arg.Is<Application.Models.Filters.CreateFilterRequest>(r => r.Value == issue.Fingerprint));
        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.IgnoreFilterId.Should().Be(realFilter.Id);
    }
}
