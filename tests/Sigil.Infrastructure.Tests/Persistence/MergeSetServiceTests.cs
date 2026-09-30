using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class MergeSetServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static IIssueActivityLogger StubActivityLogger()
    {
        var logger = Substitute.For<IIssueActivityLogger>();
        logger.LogActivityAsync(Arg.Any<int>(), Arg.Any<Domain.Enums.IssueActivityAction>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>())
            .Returns(new Domain.Entities.IssueActivity());
        return logger;
    }

    private static IIssueCache StubIssueCache() => Substitute.For<IIssueCache>();

    [Fact]
    public async Task Create_WithTwoIssues_CreatesSetAndAssignsMembership()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "Issue 1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "Issue 2");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());

        var result = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        result.Id.Should().BeGreaterThan(0);
        result.Members.Should().HaveCount(2);
        result.Members.Should().Contain(m => m.IssueId == issue1.Id);
        result.Members.Should().Contain(m => m.IssueId == issue2.Id);
        result.Members.Should().ContainSingle(m => m.IsPrimary);
    }

    [Fact]
    public async Task Create_LessThanTwoIssues_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());

        var act = () => service.CreateAsync(project.Id, [issue.Id], user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*at least 2*");
    }

    [Fact]
    public async Task Create_IssueAlreadyInMergeSet_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var issue3 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I3");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        // issue1 is already merged
        var act = () => service.CreateAsync(project.Id, [issue1.Id, issue3.Id], user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already in a merge set*");
    }

    [Fact]
    public async Task GetById_ExistingSet_ReturnsResponse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        await using var ctx2 = Ctx();
        var service2 = new MergeSetService(ctx2, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var found = await service2.GetByIdAsync(created.Id);

        found.Should().NotBeNull();
        found.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());

        (await service.GetByIdAsync(999999)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveIssue_FromThreeIssueSet_KeepsSetWithTwo()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var issue3 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I3");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id, issue3.Id], user.Id);

        await service.RemoveIssueAsync(created.Id, issue3.Id, user.Id);

        await using var verifyCtx = Ctx();
        var mergeSet = await verifyCtx.MergeSets.FindAsync(created.Id);
        mergeSet.Should().NotBeNull(); // Set still exists with 2 members

        var removedIssue = await verifyCtx.Issues.FindAsync(issue3.Id);
        removedIssue!.MergeSetId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveIssue_FromTwoIssueSet_DissolvesSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        await service.RemoveIssueAsync(created.Id, issue1.Id, user.Id);

        await using var verifyCtx = Ctx();
        var mergeSet = await verifyCtx.MergeSets.FindAsync(created.Id);
        mergeSet.Should().BeNull(); // Set dissolved

        var remaining = await verifyCtx.Issues.FindAsync(issue2.Id);
        remaining!.MergeSetId.Should().BeNull();
    }

    [Fact]
    public async Task SetPrimary_ChangesPrimaryIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        await service.SetPrimaryAsync(created.Id, issue2.Id);

        await using var verifyCtx = Ctx();
        var set = await verifyCtx.MergeSets.FindAsync(created.Id);
        set!.PrimaryIssueId.Should().Be(issue2.Id);
    }

    [Fact]
    public async Task Create_SelectsPrimaryByHighestOccurrenceCount()
    {
        // Line 33: PrimaryIssueId = issues.MaxBy(i => i.OccurrenceCount)!.Id
        // With MinBy mutation: issue with LOWEST count would be chosen as primary.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var lowCount = await TestHelper.CreateIssueAsync(ctx, project.Id, "Low");
        var highCount = await TestHelper.CreateIssueAsync(ctx, project.Id, "High");
        lowCount.OccurrenceCount = 1;
        highCount.OccurrenceCount = 100;
        await ctx.SaveChangesAsync();

        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var result = await service.CreateAsync(project.Id, [lowCount.Id, highCount.Id], user.Id);

        result.PrimaryIssueId.Should().Be(highCount.Id);
    }

    [Fact]
    public async Task Create_PropagatesStatusToNonPrimaryMembers()
    {
        // Lines 51-56: ExecuteUpdateAsync propagates primary's Status/Priority to non-primary members.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var primary = await TestHelper.CreateIssueAsync(ctx, project.Id, "Primary");
        var secondary = await TestHelper.CreateIssueAsync(ctx, project.Id, "Secondary");

        // Set primary to Resolved before merging
        primary.Status = IssueStatus.Resolved;
        primary.OccurrenceCount = 100; // ensure it's chosen as primary
        await ctx.SaveChangesAsync();

        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        await service.CreateAsync(project.Id, [primary.Id, secondary.Id], user.Id);

        await using var verifyCtx = Ctx();
        var secondaryInDb = await verifyCtx.Issues.FindAsync(secondary.Id);
        secondaryInDb!.Status.Should().Be(IssueStatus.Resolved, "non-primary member should inherit primary's status");
    }

    [Fact]
    public async Task RemoveIssue_PrimaryRemoved_AssignsNewPrimaryByEarliestFirstSeen()
    {
        // Lines 126-131: when the primary is removed, the remaining issue with earliest FirstSeen becomes new primary.
        // With mutation removing this block: PrimaryIssueId would not be updated.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var now = DateTime.UtcNow;
        var primary = await TestHelper.CreateIssueAsync(ctx, project.Id, "Primary");
        var older = await TestHelper.CreateIssueAsync(ctx, project.Id, "Older");
        var newer = await TestHelper.CreateIssueAsync(ctx, project.Id, "Newer");
        primary.OccurrenceCount = 100; // ensure it's initially the primary
        older.FirstSeen = now.AddDays(-10);
        newer.FirstSeen = now.AddDays(-1);
        await ctx.SaveChangesAsync();

        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [primary.Id, older.Id, newer.Id], user.Id);
        created.PrimaryIssueId.Should().Be(primary.Id); // confirm primary before removal

        await service.RemoveIssueAsync(created.Id, primary.Id, user.Id);

        await using var verifyCtx = Ctx();
        var mergeSet = await verifyCtx.MergeSets.FindAsync(created.Id);
        mergeSet!.PrimaryIssueId.Should().Be(older.Id, "oldest FirstSeen becomes new primary");
    }

    // ── BulkAddIssuesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task BulkAdd_AddsIssuesToExistingSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var issue3 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I3");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        await using var ctx2 = Ctx();
        var service2 = new MergeSetService(ctx2, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var result = await service2.BulkAddIssuesAsync(created.Id, [issue3.Id], user.Id);

        result.Members.Should().HaveCount(3);
        result.Members.Should().Contain(m => m.IssueId == issue3.Id);
    }

    [Fact]
    public async Task BulkAdd_EmptyList_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        await using var ctx2 = Ctx();
        var service2 = new MergeSetService(ctx2, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var act = () => service2.BulkAddIssuesAsync(created.Id, [], user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No issue IDs*");
    }

    [Fact]
    public async Task BulkAdd_IssueFromDifferentProject_Throws()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project1.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project1.Id, "I2");
        var issueOther = await TestHelper.CreateIssueAsync(ctx, project2.Id, "Other");
        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project1.Id, [issue1.Id, issue2.Id], user.Id);

        await using var ctx2 = Ctx();
        var service2 = new MergeSetService(ctx2, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var act = () => service2.BulkAddIssuesAsync(created.Id, [issueOther.Id], user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    // ── RefreshAggregatesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAggregates_RecalculatesOccurrenceCount()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        issue1.OccurrenceCount = 10;
        issue2.OccurrenceCount = 5;
        await ctx.SaveChangesAsync();

        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        // Simulate changing occurrence counts and refreshing
        await using var ctx2 = Ctx();
        var trackedIssue1 = await ctx2.Issues.FindAsync(issue1.Id);
        trackedIssue1!.OccurrenceCount = 20;
        await ctx2.SaveChangesAsync();

        var service2 = new MergeSetService(ctx2, StubActivityLogger(), StubIssueCache(), StubDateTime());
        await service2.RefreshAggregatesAsync([created.Id]);

        await using var verify = Ctx();
        var mergeSet = await verify.MergeSets.FindAsync(created.Id);
        mergeSet!.OccurrenceCount.Should().Be(25, "20 + 5");
    }

    [Fact]
    public async Task RefreshAggregates_RecalculatesMaxLevel()
    {
        // Line 177: mergeSet.Level = issues.Max(i => i.Level) — highest severity wins.
        // With Min mutation: Info would be chosen instead of Fatal.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var lowSev = await TestHelper.CreateIssueAsync(ctx, project.Id, "Low");
        var highSev = await TestHelper.CreateIssueAsync(ctx, project.Id, "High");
        lowSev.Level = Severity.Info;
        highSev.Level = Severity.Fatal;
        await ctx.SaveChangesAsync();

        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [lowSev.Id, highSev.Id], user.Id);
        await service.RefreshAggregatesAsync([created.Id]);

        await using var verifyCtx = Ctx();
        var mergeSet = await verifyCtx.MergeSets.FindAsync(created.Id);
        mergeSet!.Level.Should().Be(Severity.Fatal, "highest severity across members should win");
    }

    [Fact]
    public async Task RefreshAggregates_RecalculatesFirstAndLastSeen()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var now = DateTime.UtcNow;
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I1");
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "I2");
        issue1.FirstSeen = now.AddDays(-10);
        issue1.LastSeen = now.AddDays(-5);
        issue2.FirstSeen = now.AddDays(-3);
        issue2.LastSeen = now;
        await ctx.SaveChangesAsync();

        var service = new MergeSetService(ctx, StubActivityLogger(), StubIssueCache(), StubDateTime());
        var created = await service.CreateAsync(project.Id, [issue1.Id, issue2.Id], user.Id);

        await using var ctx2 = Ctx();
        var service2 = new MergeSetService(ctx2, StubActivityLogger(), StubIssueCache(), StubDateTime());
        await service2.RefreshAggregatesAsync([created.Id]);

        await using var verify = Ctx();
        var mergeSet = await verify.MergeSets.FindAsync(created.Id);
        mergeSet!.FirstSeen.Should().BeCloseTo(now.AddDays(-10), TimeSpan.FromSeconds(1));
        mergeSet.LastSeen.Should().BeCloseTo(now, TimeSpan.FromSeconds(5));
    }
}
