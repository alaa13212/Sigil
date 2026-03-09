using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class AppConfigServiceTests(TestDatabaseFixture fixture)
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SigilDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        return services.BuildServiceProvider();
    }

    private AppConfigService CreateService() => new(BuildProvider());

    private async Task SetConfigAsync(string key, string? value)
    {
        using var scope = BuildProvider().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var existing = await ctx.AppConfigs.FindAsync(key);
        if (existing is null)
        {
            ctx.AppConfigs.Add(new AppConfig { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }

    private async Task DeleteConfigAsync(string key)
    {
        using var scope = BuildProvider().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var existing = await ctx.AppConfigs.FindAsync(key);
        if (existing is not null)
        {
            ctx.AppConfigs.Remove(existing);
            await ctx.SaveChangesAsync();
        }
    }

    // ── LoadAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesStoreFromDb()
    {
        var uniqueKey = $"test_load_{Guid.NewGuid():N}";
        await SetConfigAsync(uniqueKey, "hello");

        var service = CreateService();
        await service.LoadAsync();

        service.Get(uniqueKey).Should().Be("hello");

        await DeleteConfigAsync(uniqueKey);
    }

    [Fact]
    public async Task Get_ExistingKey_ReturnsValue()
    {
        var key = $"test_existing_{Guid.NewGuid():N}";
        await SetConfigAsync(key, "my_value");

        var service = CreateService();
        await service.LoadAsync();

        service.Get(key).Should().Be("my_value");

        await DeleteConfigAsync(key);
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var service = CreateService();
        await service.LoadAsync();

        service.Get($"nonexistent_{Guid.NewGuid():N}").Should().BeNull();
    }

    [Fact]
    public async Task GetT_ParsesTypedValue()
    {
        var key = $"test_typed_{Guid.NewGuid():N}";
        await SetConfigAsync(key, "42");

        var service = CreateService();
        await service.LoadAsync();

        service.Get(key, 0).Should().Be(42);

        await DeleteConfigAsync(key);
    }

    [Fact]
    public async Task GetT_MissingKey_ReturnsDefault()
    {
        var service = CreateService();
        await service.LoadAsync();

        service.Get($"missing_{Guid.NewGuid():N}", 99).Should().Be(99);
    }

    [Fact]
    public async Task HostUrl_ReturnsConfiguredValue()
    {
        await SetConfigAsync(AppConfigKeys.HostUrl, "https://test.sigil.io");

        var service = CreateService();
        await service.LoadAsync();

        service.HostUrl.Should().Be("https://test.sigil.io");

        await SetConfigAsync(AppConfigKeys.HostUrl, null);
    }

    [Fact]
    public async Task SetupComplete_DefaultsFalse()
    {
        // Ensure the key doesn't exist
        await DeleteConfigAsync(AppConfigKeys.SetupComplete);

        var service = CreateService();
        await service.LoadAsync();

        service.SetupComplete.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_AfterDbMutation_ReflectsNewValue()
    {
        var key = $"test_mutation_{Guid.NewGuid():N}";
        await SetConfigAsync(key, "initial");

        var service = CreateService();
        await service.LoadAsync();
        service.Get(key).Should().Be("initial");

        // Mutate and reload
        await SetConfigAsync(key, "updated");
        await service.LoadAsync();

        service.Get(key).Should().Be("updated");

        await DeleteConfigAsync(key);
    }
}
