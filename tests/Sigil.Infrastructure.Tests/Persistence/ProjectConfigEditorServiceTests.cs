using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ProjectConfigEditorServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static IProjectConfigService StubProjectConfig()
    {
        var svc = Substitute.For<IProjectConfigService>();
        svc.LoadAsync(Arg.Any<int>()).Returns(Task.CompletedTask);
        return svc;
    }

    [Fact]
    public async Task SetAsync_NewKey_CreatesEntry()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new ProjectConfigEditorService(ctx, StubProjectConfig(), StubDateTime());

        await service.SetAsync(project.Id, "my_key", "my_value");

        await using var verifyCtx = Ctx();
        var entry = await verifyCtx.ProjectConfigs
            .FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Key == "my_key");
        entry.Should().NotBeNull();
        entry.Value.Should().Be("my_value");
    }

    [Fact]
    public async Task SetAsync_ExistingKey_UpdatesValue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new ProjectConfigEditorService(ctx, StubProjectConfig(), StubDateTime());

        await service.SetAsync(project.Id, "key", "original");
        await service.SetAsync(project.Id, "key", "updated");

        await using var verifyCtx = Ctx();
        var entry = await verifyCtx.ProjectConfigs
            .FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Key == "key");
        entry!.Value.Should().Be("updated");
    }

    [Fact]
    public async Task SetAsync_CallsLoadAsync()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var configService = StubProjectConfig();
        var service = new ProjectConfigEditorService(ctx, configService, StubDateTime());

        await service.SetAsync(project.Id, "key", "val");

        await configService.Received(1).LoadAsync(project.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsProjectEntries()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new ProjectConfigEditorService(ctx, StubProjectConfig(), StubDateTime());
        await service.SetAsync(project.Id, "k1", "v1");
        await service.SetAsync(project.Id, "k2", "v2");

        var all = await service.GetAllAsync(project.Id);

        all.Should().ContainKey("k1").WhoseValue.Should().Be("v1");
        all.Should().ContainKey("k2").WhoseValue.Should().Be("v2");
    }

    [Fact]
    public async Task GetAllAsync_IsolatedPerProject()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var service = new ProjectConfigEditorService(ctx, StubProjectConfig(), StubDateTime());
        await service.SetAsync(project1.Id, "shared_key", "val1");
        await service.SetAsync(project2.Id, "shared_key", "val2");

        var all1 = await service.GetAllAsync(project1.Id);
        var all2 = await service.GetAllAsync(project2.Id);

        all1["shared_key"].Should().Be("val1");
        all2["shared_key"].Should().Be("val2");
    }
}
