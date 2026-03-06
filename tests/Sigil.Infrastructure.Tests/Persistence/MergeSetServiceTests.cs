using Microsoft.EntityFrameworkCore;
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
}
