using Sigil.Application.Interfaces;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class SharedLinkServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(DateTime.UtcNow);
        return dt;
    }

    [Fact]
    public async Task CreateLink_PersistsAndReturnsUrl()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issueService = Substitute.For<IIssueService>();
        var eventService = Substitute.For<IEventService>();
        var service = new SharedLinkService(ctx, issueService, eventService, StubDateTime());

        var result = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        result.Token.Should().NotBe(Guid.Empty);
        result.IssueId.Should().Be(issue.Id);
        result.Url.Should().Contain(result.Token.ToString());
        result.ExpiresAt.Should().BeAfter(result.CreatedAt);
    }

    [Fact]
    public async Task CreateLink_DefaultDuration_24Hours()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        var result = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        var expectedExpiry = result.CreatedAt.AddHours(24);
        result.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateLink_CustomDuration_Respected()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        var result = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test", TimeSpan.FromHours(1));

        var expectedExpiry = result.CreatedAt.AddHours(1);
        result.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateLink_MaxDuration_CappedAt7Days()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        var result = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test", TimeSpan.FromDays(30));

        var maxExpiry = result.CreatedAt.AddDays(7);
        result.ExpiresAt.Should().BeCloseTo(maxExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RevokeLinkAsync_DeletesLink()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());
        var link = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        var revoked = await service.RevokeLinkAsync(link.Token);

        revoked.Should().BeTrue();

        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.SharedIssueLinks.FindAsync(link.Token);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task RevokeLinkAsync_NonExistentToken_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        (await service.RevokeLinkAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLink_ExpiredToken_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        // Insert an already-expired link directly
        var expiredToken = Guid.NewGuid();
        ctx.SharedIssueLinks.Add(new Domain.Entities.SharedIssueLink
        {
            Token = expiredToken,
            IssueId = issue.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        });
        await ctx.SaveChangesAsync();

        var result = await service.ValidateLinkAsync(expiredToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateLink_ValidToken_ReturnsData()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issueService = Substitute.For<IIssueService>();
        issueService.GetIssueDetailAsync(issue.Id).Returns(new IssueDetailResponse(
            issue.Id, project.Id, "Test", "TestException", null, issue.Fingerprint,
            IssueStatus.Open, Priority.Medium, Severity.Error,
            DateTime.UtcNow, DateTime.UtcNow, 1, null, null, null, null,
            [], null, null, null));
        var eventService = Substitute.For<IEventService>();
        var service = new SharedLinkService(ctx, issueService, eventService, StubDateTime());
        var link = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        var result = await service.ValidateLinkAsync(link.Token);

        result.Should().NotBeNull();
        result.Issue.Id.Should().Be(issue.Id);
    }

    // ── GetSharedEventsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSharedEvents_ValidToken_ReturnsPaginatedEvents()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var link = await new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime())
            .CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        await using var ctx2 = Ctx();
        var eventService = Substitute.For<IEventService>();
        var fakePage = new Application.Models.PagedResponse<Application.Models.Events.EventSummary>([], 0, 1, 20);
        eventService.GetEventSummariesAsync(issue.Id, 1, 20).Returns(fakePage);
        var service = new SharedLinkService(ctx2, Substitute.For<IIssueService>(), eventService, StubDateTime());

        var result = await service.GetSharedEventsAsync(link.Token, 1, 20);

        result.Should().NotBeNull();
        await eventService.Received(1).GetEventSummariesAsync(issue.Id, 1, 20);
    }

    [Fact]
    public async Task GetSharedEvents_ExpiredToken_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var expiredToken = Guid.NewGuid();
        ctx.SharedIssueLinks.Add(new Domain.Entities.SharedIssueLink
        {
            Token = expiredToken,
            IssueId = issue.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = new SharedLinkService(ctx2, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        var result = await service.GetSharedEventsAsync(expiredToken, 1, 20);

        result.Should().BeNull();
    }

    // ── GetSharedEventDetailAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetSharedEventDetail_ValidToken_DelegatesToEventService()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var link = await new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime())
            .CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        await using var ctx2 = Ctx();
        var eventService = Substitute.For<IEventService>();
        var service = new SharedLinkService(ctx2, Substitute.For<IIssueService>(), eventService, StubDateTime());

        await service.GetSharedEventDetailAsync(link.Token, 1L);

        await eventService.Received(1).GetIssueEventDetailAsync(issue.Id, 1L);
    }

    [Fact]
    public async Task GetSharedEventDetail_ExpiredToken_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var expiredToken = Guid.NewGuid();
        ctx.SharedIssueLinks.Add(new Domain.Entities.SharedIssueLink
        {
            Token = expiredToken,
            IssueId = issue.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = new SharedLinkService(ctx2, Substitute.For<IIssueService>(), Substitute.For<IEventService>(), StubDateTime());

        var result = await service.GetSharedEventDetailAsync(expiredToken, 1L);

        result.Should().BeNull();
    }
}
