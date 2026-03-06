using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class AppConfigEditorServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IAppConfigService StubAppConfig()
    {
        var svc = Substitute.For<IAppConfigService>();
        svc.LoadAsync().Returns(Task.CompletedTask);
        return svc;
    }

    [Fact]
    public async Task SetAsync_NewKey_CreatesEntry()
    {
        var key = $"test_key_{Guid.NewGuid():N}";
        await using var ctx = Ctx();
        var appConfig = StubAppConfig();
        var service = new AppConfigEditorService(ctx, appConfig);

        await service.SetAsync(key, "my-value");

        await using var verifyCtx = Ctx();
        var entry = await verifyCtx.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        entry.Should().NotBeNull();
        entry.Value.Should().Be("my-value");
    }

    [Fact]
    public async Task SetAsync_ExistingKey_UpdatesValue()
    {
        var key = $"test_key_{Guid.NewGuid():N}";
        await using var ctx = Ctx();
        var appConfig = StubAppConfig();
        var service = new AppConfigEditorService(ctx, appConfig);

        await service.SetAsync(key, "original");
        await service.SetAsync(key, "updated");

        await using var verifyCtx = Ctx();
        var entry = await verifyCtx.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        entry!.Value.Should().Be("updated");
    }

    [Fact]
    public async Task SetAsync_CallsLoadAsync()
    {
        var key = $"test_key_{Guid.NewGuid():N}";
        await using var ctx = Ctx();
        var appConfig = StubAppConfig();
        var service = new AppConfigEditorService(ctx, appConfig);

        await service.SetAsync(key, "value");

        await appConfig.Received(1).LoadAsync();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        var key = $"test_key_{Guid.NewGuid():N}";
        await using var ctx = Ctx();
        var service = new AppConfigEditorService(ctx, StubAppConfig());
        await service.SetAsync(key, "value");

        var all = await service.GetAllAsync();

        all.Should().ContainKey(key);
        all[key].Should().Be("value");
    }

    [Fact]
    public async Task SetAsync_NullValue_StoresNull()
    {
        var key = $"test_key_{Guid.NewGuid():N}";
        await using var ctx = Ctx();
        var service = new AppConfigEditorService(ctx, StubAppConfig());

        await service.SetAsync(key, null);

        await using var verifyCtx = Ctx();
        var entry = await verifyCtx.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        entry.Should().NotBeNull();
        entry.Value.Should().BeNull();
    }
}
