using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;

namespace Sigil.Server.Auth.Saml;

internal sealed class SamlMetadataRefreshWorker(
    IServiceScopeFactory scopeFactory,
    SamlConnectionRegistry registry,
    ILogger<SamlMetadataRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The SAML metadata refresh failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var connectionService = scope.ServiceProvider.GetRequiredService<ISamlConnectionService>();
        var connections = await connectionService.GetConnectionsAsync();

        foreach (var connection in connections)
        {
            if (!connection.IsEnabled || string.IsNullOrWhiteSpace(connection.MetadataUrl)) continue;

            await connectionService.RefreshMetadataAsync(connection.Id);
        }

        await registry.RefreshAsync();
    }
}
