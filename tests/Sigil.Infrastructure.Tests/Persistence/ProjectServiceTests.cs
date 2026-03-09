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

    [Fact]
    public async Task CreateProject_PersistsWithApiKey()
    {
        await using var context = CreateContext();
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());

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
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
        var created = await service.CreateProjectAsync("GetById Test", Platform.Python);

        await using var context2 = CreateContext();
        var service2 = new ProjectService(context2, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
        var found = await service2.GetProjectByIdAsync(created.Id);

        found.Should().NotBeNull();
        found.Name.Should().Be("GetById Test");
    }

    [Fact]
    public async Task GetProjectById_NonExistent_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());

        var found = await service.GetProjectByIdAsync(999999);

        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProject_ChangesName()
    {
        await using var context = CreateContext();
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
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
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
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
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());

        var deleted = await service.DeleteProjectAsync(999999);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task RotateApiKey_ReturnsNewKey()
    {
        await using var context = CreateContext();
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
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
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
        await service.CreateProjectAsync("All-Test-1", Platform.CSharp);
        await service.CreateProjectAsync("All-Test-2", Platform.Python);

        var all = await service.GetAllProjectsAsync();

        all.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetProjectDetail_ReturnsDsn()
    {
        await using var context = CreateContext();
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
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
        var service = new ProjectService(context, StubAppConfig(), StubNormEngine(), StubProjectCache(), StubDateTime());
        var created = await service.CreateProjectAsync("ConfigSeed Test", Platform.CSharp);

        await using var verifyCtx = CreateContext();
        var configs = await verifyCtx.ProjectConfigs
            .Where(c => c.ProjectId == created.Id)
            .ToListAsync();

        configs.Should().ContainSingle(c => c.Key == ProjectConfigKeys.HighVolumeThreshold);
    }
}
