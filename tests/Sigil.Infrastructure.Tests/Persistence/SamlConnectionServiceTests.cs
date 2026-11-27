using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class SamlConnectionServiceTests(TestDatabaseFixture fixture)
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SigilDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        services.AddHttpClient();
        return services.BuildServiceProvider();
    }

    private SamlConnectionService CreateService()
    {
        var provider = BuildProvider();
        return new SamlConnectionService(
            provider.GetRequiredService<SigilDbContext>(),
            new DateTimeProvider(),
            Substitute.For<IAppConfigService>(),
            provider.GetRequiredService<IHttpClientFactory>());
    }

    private static SamlConnectionRequest CreateRequest(string name) => new(
        name,
        $"https://idp.example.com/{name}/metadata",
        $"{name} display",
        $"https://idp.example.com/{name}/metadata");

    [Fact]
    public async Task CreateConnectionAsync_PersistsConnectionWithMappings()
    {
        var service = CreateService();
        var name = $"saml_{Guid.NewGuid():N}";

        var request = CreateRequest(name) with
        {
            GroupMappings =
            [
                new SamlGroupMappingRequest("engineering", "Engineering", TeamRole.Admin, true)
            ]
        };

        var created = await service.CreateConnectionAsync(request);

        created.Name.Should().Be(name);
        created.EntityId.Should().Be(request.EntityId);
        created.GroupMappings.Should().ContainSingle();
        created.GroupMappings[0].GroupName.Should().Be("engineering");
        created.GroupMappings[0].Role.Should().Be(TeamRole.Admin);
        created.GroupMappings[0].GrantsGlobalAdmin.Should().BeTrue();

        await service.DeleteConnectionAsync(created.Id);
    }

    [Fact]
    public async Task UpdateConnectionAsync_ReplacesGroupMappings()
    {
        var service = CreateService();
        var name = $"saml_{Guid.NewGuid():N}";

        var created = await service.CreateConnectionAsync(CreateRequest(name) with
        {
            GroupMappings = [new SamlGroupMappingRequest("old", "Old")]
        });

        var updated = await service.UpdateConnectionAsync(created.Id, CreateRequest(name) with
        {
            GroupMappings = [new SamlGroupMappingRequest("new", "New", TeamRole.Owner)]
        });

        updated.Should().NotBeNull();
        updated!.GroupMappings.Should().ContainSingle();
        updated.GroupMappings[0].GroupName.Should().Be("new");
        updated.GroupMappings[0].Role.Should().Be(TeamRole.Owner);

        await service.DeleteConnectionAsync(created.Id);
    }

    [Fact]
    public async Task DeleteConnectionAsync_RemovesConnection()
    {
        var service = CreateService();
        var created = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}"));

        (await service.DeleteConnectionAsync(created.Id)).Should().BeTrue();
        (await service.DeleteConnectionAsync(created.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task SetMetadataAsync_WithXml_StoresMetadata()
    {
        var service = CreateService();
        var created = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}"));

        var fetchedAt = DateTime.UtcNow;
        (await service.SetMetadataAsync(created.Id, "<EntityDescriptor />", fetchedAt, null)).Should().BeTrue();

        var reloaded = await service.GetConnectionAsync(created.Id);
        reloaded.Should().NotBeNull();
        reloaded!.HasMetadata.Should().BeTrue();
        reloaded.MetadataFetchedAt.Should().Be(fetchedAt);
        reloaded.MetadataError.Should().BeNull();

        await service.DeleteConnectionAsync(created.Id);
    }

    [Fact]
    public async Task SetMetadataAsync_WithError_KeepsExistingMetadata()
    {
        var service = CreateService();
        var created = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}"));

        await service.SetMetadataAsync(created.Id, "<EntityDescriptor />", DateTime.UtcNow, null);
        await service.SetMetadataAsync(created.Id, null, null, "boom");

        var reloaded = await service.GetConnectionAsync(created.Id);
        reloaded.Should().NotBeNull();
        reloaded!.HasMetadata.Should().BeTrue();
        reloaded.MetadataError.Should().Be("boom");

        await service.DeleteConnectionAsync(created.Id);
    }

    [Fact]
    public async Task GetEnabledConnectionsAsync_ExcludesDisabledConnections()
    {
        var service = CreateService();
        var enabled = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}"));
        var disabled = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}") with { IsEnabled = false });

        var available = await service.GetEnabledConnectionsAsync();

        available.Should().Contain(c => c.Id == enabled.Id);
        available.Should().NotContain(c => c.Id == disabled.Id);

        await service.DeleteConnectionAsync(enabled.Id);
        await service.DeleteConnectionAsync(disabled.Id);
    }

    [Fact]
    public async Task GetRuntimeConfigurationsAsync_ReturnsEnabledConnectionsWithMetadata()
    {
        var service = CreateService();
        var created = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}"));
        await service.SetMetadataAsync(created.Id, "<EntityDescriptor />", DateTime.UtcNow, null);

        var configs = await service.GetRuntimeConfigurationsAsync();

        configs.Should().ContainSingle(c => c.Id == created.Id);
        configs.First(c => c.Id == created.Id).MetadataXml.Should().Be("<EntityDescriptor />");

        await service.DeleteConnectionAsync(created.Id);
    }

    [Fact]
    public async Task GetEndpointsAsync_UsesConfiguredHostUrl()
    {
        var provider = BuildProvider();
        var configService = Substitute.For<IAppConfigService>();
        configService.HostUrl.Returns("https://sigil.example.com/");

        var service = new SamlConnectionService(
            provider.GetRequiredService<SigilDbContext>(),
            new DateTimeProvider(),
            configService,
            provider.GetRequiredService<IHttpClientFactory>());

        var endpoints = await service.GetEndpointsAsync();

        endpoints.EntityId.Should().Be("https://sigil.example.com/saml/sp");
        endpoints.MetadataUrl.Should().Be("https://sigil.example.com/saml/sp");
        endpoints.AcsUrl.Should().Be("https://sigil.example.com/saml/sp/Acs");
    }

    [Fact]
    public async Task GetPendingUsersAsync_ReturnsPendingSamlUsers()
    {
        var provider = BuildProvider();
        var service = CreateService();

        var created = await service.CreateConnectionAsync(CreateRequest($"saml_{Guid.NewGuid():N}"));

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"pending_{Guid.NewGuid():N}@example.com",
            Email = $"pending_{Guid.NewGuid():N}@example.com",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            SamlStatus = SamlUserStatus.Pending,
            SamlConnectionId = created.Id
        };

        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
        }

        var pending = await service.GetPendingUsersAsync();
        pending.Should().Contain(p => p.UserId == user.Id);

        (await service.ApproveUserAsync(user.Id)).Should().BeTrue();

        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            var stored = await ctx.Users.AsTracking().FirstAsync(u => u.Id == user.Id);
            stored.SamlStatus.Should().Be(SamlUserStatus.Approved);
        }

        (await service.RejectUserAsync(user.Id)).Should().BeTrue();

        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            var stored = await ctx.Users.AsTracking().FirstAsync(u => u.Id == user.Id);
            stored.SamlStatus.Should().Be(SamlUserStatus.Rejected);
        }

        await service.DeleteConnectionAsync(created.Id);
    }
}
