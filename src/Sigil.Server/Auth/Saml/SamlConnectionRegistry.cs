using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;
using Sustainsys.Saml2.Metadata;

namespace Sigil.Server.Auth.Saml;

public sealed record SamlConnectionDescriptor(Guid Id, string EntityId, EntityDescriptor Metadata);

public sealed record SamlSnapshot(IReadOnlyList<SamlConnectionDescriptor> Connections)
{
    public static SamlSnapshot Empty { get; } = new([]);
}

public sealed class SamlConnectionRegistry(
    IServiceScopeFactory scopeFactory,
    Saml2OptionsCache optionsCache,
    ILogger<SamlConnectionRegistry> logger) : IAsyncStartupInitializer
{
    private SamlSnapshot snapshot = SamlSnapshot.Empty;

    public SamlSnapshot Current => Volatile.Read(ref snapshot);

    public Task InitializeAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var configurationService = scope.ServiceProvider.GetRequiredService<ISamlRuntimeConfigurationService>();
        var connectionService = scope.ServiceProvider.GetRequiredService<ISamlConnectionService>();

        var configs = await configurationService.GetRuntimeConfigurationsAsync();
        var connections = new List<SamlConnectionDescriptor>();

        foreach (var config in configs)
        {
            if (string.IsNullOrWhiteSpace(config.MetadataXml))
            {
                logger.LogWarning("SAML connection {EntityId} has no metadata and was skipped.", config.EntityId);
                continue;
            }

            try
            {
                var metadata = SamlMetadataReader.Read(config.MetadataXml, config.EntityId);
                connections.Add(new SamlConnectionDescriptor(config.Id, config.EntityId, metadata));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The metadata for SAML connection {EntityId} could not be read.", config.EntityId);

                await connectionService.SetMetadataAsync(config.Id, null, null, ex.Message);
            }
        }

        Volatile.Write(ref snapshot, new SamlSnapshot(connections));

        optionsCache.Invalidate();
    }
}
