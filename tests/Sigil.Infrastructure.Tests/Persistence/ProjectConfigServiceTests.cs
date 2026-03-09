using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ProjectConfigServiceTests(TestDatabaseFixture fixture)
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SigilDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        return services.BuildServiceProvider();
    }

    private ProjectConfigService CreateService() => new(BuildProvider());

    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private async Task SetProjectConfigAsync(int projectId, string key, string? value)
    {
        await using var ctx = Ctx();
        var existing = await ctx.ProjectConfigs
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Key == key);
        if (existing is null)
        {
            ctx.ProjectConfigs.Add(new ProjectConfig
            {
                ProjectId = projectId, Key = key, Value = value, UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }

    private async Task DeleteProjectConfigAsync(int projectId, string key)
    {
        await using var ctx = Ctx();
        var existing = await ctx.ProjectConfigs
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Key == key);
        if (existing is not null)
        {
            ctx.ProjectConfigs.Remove(existing);
            await ctx.SaveChangesAsync();
        }
    }

    // ── LoadAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesPerProjectStore()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold, "2000");

        var service = CreateService();
        await service.LoadAsync();

        service.HighVolumeThreshold(project.Id).Should().Be(2000);
    }

    [Fact]
    public async Task HighVolumeThreshold_ReturnsConfiguredValue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold, "500");

        var service = CreateService();
        await service.LoadAsync();

        service.HighVolumeThreshold(project.Id).Should().Be(500);
    }

    [Fact]
    public async Task HighVolumeThreshold_MissingConfig_ReturnsDefault1000()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await DeleteProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold);

        var service = CreateService();
        await service.LoadAsync();

        service.HighVolumeThreshold(project.Id).Should().Be(1000);
    }

    [Fact]
    public async Task RetentionMaxAgeDays_ReturnsNullWhenNotSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await DeleteProjectConfigAsync(project.Id, ProjectConfigKeys.RetentionMaxAgeDays);

        var service = CreateService();
        await service.LoadAsync();

        service.RetentionMaxAgeDays(project.Id).Should().BeNull();
    }

    [Fact]
    public async Task RetentionMaxAgeDays_ReturnsValueWhenSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.RetentionMaxAgeDays, "30");

        var service = CreateService();
        await service.LoadAsync();

        service.RetentionMaxAgeDays(project.Id).Should().Be(30);
    }

    [Fact]
    public async Task GetNullable_MissingKey_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var service = CreateService();
        await service.LoadAsync();

        service.RetentionMaxEventCount(project.Id).Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ProjectId_OnlyReloadsOneProject()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold, "777");

        var service = CreateService();
        await service.LoadAsync();
        service.HighVolumeThreshold(project.Id).Should().Be(777);

        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold, "888");
        await service.LoadAsync(project.Id);

        service.HighVolumeThreshold(project.Id).Should().Be(888);
    }

    [Fact]
    public async Task LoadAsync_AfterDbMutation_ReflectsNewValue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold, "100");

        var service = CreateService();
        await service.LoadAsync();
        service.HighVolumeThreshold(project.Id).Should().Be(100);

        await SetProjectConfigAsync(project.Id, ProjectConfigKeys.HighVolumeThreshold, "200");
        await service.LoadAsync();

        service.HighVolumeThreshold(project.Id).Should().Be(200);
    }
}
