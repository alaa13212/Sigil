using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class IssueActivityServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    [Fact]
    public async Task LogActivity_PersistsAndReturnsActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new IssueActivityService(ctx, StubDateTime());

        var activity = await service.LogActivityAsync(issue.Id, IssueActivityAction.Created);

        activity.Id.Should().BeGreaterThan(0);
        activity.IssueId.Should().Be(issue.Id);
        activity.Action.Should().Be(IssueActivityAction.Created);
    }

    [Fact]
    public async Task LogActivity_WithUserAndMessage_StoresAllFields()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new IssueActivityService(ctx, StubDateTime());

        var activity = await service.LogActivityAsync(issue.Id, IssueActivityAction.Commented, user.Id, "A comment",
            new Dictionary<string, string> { ["key"] = "val" });

        activity.UserId.Should().Be(user.Id);
        activity.Message.Should().Be("A comment");
        activity.Extra.Should().ContainKey("key");
    }

    [Fact]
    public async Task LogActivity_UpdatesIssueLastChangedAt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var originalChanged = issue.LastChangedAt;
        var service = new IssueActivityService(ctx, StubDateTime());

        await service.LogActivityAsync(issue.Id, IssueActivityAction.Resolved);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.LastChangedAt.Should().Be(new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetActivities_ReturnsPaginatedDescending()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new IssueActivityService(ctx, StubDateTime());

        await service.LogActivityAsync(issue.Id, IssueActivityAction.Created);
        await service.LogActivityAsync(issue.Id, IssueActivityAction.Resolved);
        await service.LogActivityAsync(issue.Id, IssueActivityAction.Commented, message: "test");

        var (items, totalCount) = await service.GetActivitiesForIssueAsync(issue.Id);

        totalCount.Should().Be(3);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActivitySummaries_ReturnsMappedResponses()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new IssueActivityService(ctx, StubDateTime());
        await service.LogActivityAsync(issue.Id, IssueActivityAction.Created);

        var result = await service.GetActivitySummariesAsync(issue.Id);

        result.Items.Should().HaveCount(1);
        result.Items[0].Action.Should().Be(IssueActivityAction.Created);
        result.TotalCount.Should().Be(1);
    }
}
