using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ProjectServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new SigilDbContext(options);
    }

    private static IProjectCache StubProjectCache()
    {
        var cache = Substitute.For<IProjectCache>();
        cache.TryGet(Arg.Any<int>(), out Arg.Any<Project?>()).Returns(false);
        cache.TryGetList(out Arg.Any<List<Project>?>()).Returns(false);
        return cache;
    }

    private static IAppConfigService StubAppConfig()
    {
        var config = Substitute.For<IAppConfigService>();
        config.HostUrl.Returns("https://sigil.example.com");
        return config;
    }

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static INormalizationRuleEngine StubNormEngine()
    {
        var engine = Substitute.For<INormalizationRuleEngine>();
        engine.CreateDefaultRulesPreset().Returns([]);
        return engine;
    }

    private static ProjectService CreateService(SigilDbContext context, IProjectAccessService? access = null)
        => new(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime(),
            access ?? new StubProjectAccessService());

    [Fact]
    public async Task CreateProject_PersistsWithApiKey()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var project = await service.CreateProjectAsync("IntegrationTest Project", Platform.CSharp);

        project.Id.Should().BeGreaterThan(0);
        project.Name.Should().Be("IntegrationTest Project");
        project.Platform.Should().Be(Platform.CSharp);
        project.ApiKey.Should().NotBeNullOrEmpty();
        project.ApiKey.Should().HaveLength(32);
    }

    [Fact]
    public async Task GetProjectById_ExistingProject_ReturnsProject()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateProjectAsync("GetById Test", Platform.Python);

        await using var context2 = CreateContext();
        var service2 = CreateService(context2);
        var found = await service2.GetProjectByIdAsync(created.Id);

        found.Should().NotBeNull();
        found.Name.Should().Be("GetById Test");
    }

    [Fact]
    public async Task GetProjectById_NonExistent_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var found = await service.GetProjectByIdAsync(999999);

        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProject_ChangesName()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateProjectAsync("Before Rename", Platform.JavaScript);

        var updated = await service.UpdateProjectAsync(created.Id, "After Rename");

        updated.Name.Should().Be("After Rename");

        // Verify in DB
        await using var verifyCtx = CreateContext();
        var inDb = await verifyCtx.Projects.FindAsync(created.Id);
        inDb!.Name.Should().Be("After Rename");
    }

    [Fact]
    public async Task DeleteProject_RemovesFromDatabase()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateProjectAsync("ToDelete", Platform.Go);

        var deleted = await service.DeleteProjectAsync(created.Id);

        deleted.Should().BeTrue();

        await using var verifyCtx = CreateContext();
        var inDb = await verifyCtx.Projects.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProject_NonExistentId_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var deleted = await service.DeleteProjectAsync(999999);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task RotateApiKey_ReturnsNewKey()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateProjectAsync("RotateKey Test", Platform.CSharp);
        var originalKey = created.ApiKey;

        var newKey = await service.RotateApiKeyAsync(created.Id);

        newKey.Should().NotBeNullOrEmpty();
        newKey.Should().NotBe(originalKey);
        newKey.Should().HaveLength(32);
    }

    [Fact]
    public async Task GetAllProjects_ReturnsAll()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.CreateProjectAsync("All-Test-1", Platform.CSharp);
        await service.CreateProjectAsync("All-Test-2", Platform.Python);

        var all = await service.GetAllProjectsAsync();

        all.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetProjectDetail_ReturnsDsn()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateProjectAsync("DSN Test", Platform.CSharp);

        var detail = await service.GetProjectDetailAsync(created.Id);

        detail.Should().NotBeNull();
        detail.Dsn.Should().Contain(created.ApiKey);
        detail.Dsn.Should().Contain(created.Id.ToString());
        detail.Dsn.Should().StartWith("https://");
    }

    [Fact]
    public async Task CreateProject_SeedsDefaultProjectConfig()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateProjectAsync("ConfigSeed Test", Platform.CSharp);

        await using var verifyCtx = CreateContext();
        var configs = await verifyCtx.ProjectConfigs
            .Where(c => c.ProjectId == created.Id)
            .ToListAsync();

        configs.Should().ContainSingle(c => c.Key == ProjectConfigKeys.HighVolumeThreshold);
    }

    // ── GetAllProjectOverviewsAsync ──────────────────────────────────────────

    private static async Task<Issue> CreateIssueSeenAsync(SigilDbContext context, int projectId, DateTime firstSeen)
    {
        var issue = await TestHelper.CreateIssueAsync(context, projectId);
        issue.FirstSeen = firstSeen;
        issue.LastSeen = firstSeen;
        issue.LastChangedAt = firstSeen;
        await context.SaveChangesAsync();
        return issue;
    }

    [Fact]
    public async Task Overviews_ListOnlyTheProjectsTheUserCanSee()
    {
        await using var context = CreateContext();
        var visible = await TestHelper.CreateProjectAsync(context);
        await TestHelper.CreateProjectAsync(context);
        var service = CreateService(context, new StubProjectAccessService(visible.Id));

        var overviews = await service.GetAllProjectOverviewsAsync();

        overviews.Should().ContainSingle();
        overviews[0].Id.Should().Be(visible.Id);
    }

    [Fact]
    public async Task Overviews_CountTheIssuesAndEventsInsideTheWindow()
    {
        await using var context = CreateContext();
        var project = await TestHelper.CreateProjectAsync(context);
        await CreateIssueSeenAsync(context, project.Id, new DateTime(2025, 5, 30, 0, 0, 0, DateTimeKind.Utc));
        await CreateIssueSeenAsync(context, project.Id, new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var inside = await TestHelper.CreateIssueAsync(context, project.Id);
        inside.FirstSeen = new DateTime(2025, 5, 31, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();
        await TestHelper.CreateEventAsync(context, project.Id, inside.Id,
            timestamp: new DateTime(2025, 5, 31, 12, 0, 0, DateTimeKind.Utc));
        await TestHelper.CreateEventAsync(context, project.Id, inside.Id,
            timestamp: new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, new StubProjectAccessService(project.Id));

        var overview = (await service.GetAllProjectOverviewsAsync(7)).Single();

        overview.IssueCount.Should().Be(3);
        overview.EventCount.Should().Be(2);
        overview.WindowDays.Should().Be(7);
        overview.IssuesInWindow.Should().Be(2);
        overview.EventsInWindow.Should().Be(1);
    }

    [Fact]
    public async Task Overviews_ReportZeroForAProjectWithoutEventsInTheWindow()
    {
        await using var context = CreateContext();
        var project = await TestHelper.CreateProjectAsync(context);
        var issue = await TestHelper.CreateIssueAsync(context, project.Id);
        await TestHelper.CreateEventAsync(context, project.Id, issue.Id,
            timestamp: new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, new StubProjectAccessService(project.Id));

        var overview = (await service.GetAllProjectOverviewsAsync(7)).Single();

        overview.EventsInWindow.Should().Be(0);
        overview.EventCount.Should().Be(1);
    }

    [Fact]
    public async Task Overviews_LifetimeCountsDoNotDependOnTheWindow()
    {
        await using var context = CreateContext();
        var project = await TestHelper.CreateProjectAsync(context);
        var issue = await TestHelper.CreateIssueAsync(context, project.Id);
        await TestHelper.CreateEventAsync(context, project.Id, issue.Id,
            timestamp: new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, new StubProjectAccessService(project.Id));

        var wide = (await service.GetAllProjectOverviewsAsync(30)).Single();
        var narrow = (await service.GetAllProjectOverviewsAsync(1)).Single();

        wide.IssueCount.Should().Be(narrow.IssueCount);
        wide.EventCount.Should().Be(narrow.EventCount);
        wide.WindowDays.Should().Be(30);
        narrow.WindowDays.Should().Be(1);
    }

    [Fact]
    public async Task Overviews_WithoutAVisibleProject_AreEmpty()
    {
        await using var context = CreateContext();
        await TestHelper.CreateProjectAsync(context);
        var service = CreateService(context, new StubProjectAccessService());

        var overviews = await service.GetAllProjectOverviewsAsync();

        overviews.Should().BeEmpty();
    }

    [Fact]
    public async Task Overviews_OrderTheProjectsWithTheMostRecentActivityFirst()
    {
        await using var context = CreateContext();
        var quiet = await TestHelper.CreateProjectAsync(context);
        var busy = await TestHelper.CreateProjectAsync(context);
        var quietIssue = await TestHelper.CreateIssueAsync(context, quiet.Id);
        var busyIssue = await TestHelper.CreateIssueAsync(context, busy.Id);
        await TestHelper.CreateEventAsync(context, quiet.Id, quietIssue.Id,
            timestamp: new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        await TestHelper.CreateEventAsync(context, busy.Id, busyIssue.Id,
            timestamp: new DateTime(2025, 5, 31, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, new StubProjectAccessService(quiet.Id, busy.Id));

        var overviews = await service.GetAllProjectOverviewsAsync();

        overviews.Select(o => o.Id).Should().ContainInOrder(busy.Id, quiet.Id);
    }
}
