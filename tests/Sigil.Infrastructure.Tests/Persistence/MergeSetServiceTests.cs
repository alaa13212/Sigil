using Sigil.Application.Interfaces;
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
