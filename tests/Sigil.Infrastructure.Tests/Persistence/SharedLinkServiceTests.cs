using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task CreateLink_PersistsAndReturnsUrl()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issueService = Substitute.For<IIssueService>();
        var eventService = Substitute.For<IEventService>();
        var service = new SharedLinkService(ctx, issueService, eventService);

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
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>());

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
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>());

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
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>());

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
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>());
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
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>());

        (await service.RevokeLinkAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLink_ExpiredToken_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = new SharedLinkService(ctx, Substitute.For<IIssueService>(), Substitute.For<IEventService>());

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
        var service = new SharedLinkService(ctx, issueService, eventService);
        var link = await service.CreateLinkAsync(issue.Id, user.Id, "https://sigil.test");

        var result = await service.ValidateLinkAsync(link.Token);

        result.Should().NotBeNull();
        result.Issue.Id.Should().Be(issue.Id);
    }
}
