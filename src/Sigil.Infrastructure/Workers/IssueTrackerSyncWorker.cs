using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Integrations;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Workers;

internal class IssueTrackerSyncWorker(
    IServiceProvider services,
    IDateTime dateTime,
    ILogger<IssueTrackerSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(StartupDelay, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncAllAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "IssueTrackerSyncWorker encountered an error");
            }

            try
            {
                await Task.Delay(SyncInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task SyncAllAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var tokenEncryption = scope.ServiceProvider.GetRequiredService<TokenEncryptionService>();
        var clients = scope.ServiceProvider.GetRequiredService<IEnumerable<IIssueTrackerClient>>();

        var links = await dbContext.ExternalIssueLinks
            .Include(l => l.Issue)
            .Where(l => dbContext.IssueTrackerConfigs
                .Any(c => c.ProjectId == l.Issue!.ProjectId &&
                          c.TrackerType == l.TrackerType &&
                          c.TwoWaySync &&
                          c.Enabled))
            .ToListAsync(ct);

        foreach (var link in links)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var config = await dbContext.IssueTrackerConfigs
                    .FirstOrDefaultAsync(c => c.ProjectId == link.Issue!.ProjectId &&
                                              c.TrackerType == link.TrackerType, ct);

                if (config == null) continue;

                var client = clients.FirstOrDefault(c => c.TrackerType == link.TrackerType);
                if (client == null) continue;

                var trackerConfig = new TrackerConfig(link.TrackerType, tokenEncryption.Decrypt(config.EncryptedConfig));
                var status = await client.GetStatusAsync(trackerConfig, link.ExternalId);
                if (status == null) continue;

                var sigilResolved = link.Issue?.Status is Domain.Enums.IssueStatus.Resolved
                    or Domain.Enums.IssueStatus.Ignored;

                // Sigil → external: if Sigil issue is resolved but external is still open, close it
                if (sigilResolved && !status.IsCompleted)
                {
                    await client.CloseIssueAsync(trackerConfig, link.ExternalId);
                    // Re-fetch status after closing
                    status = await client.GetStatusAsync(trackerConfig, link.ExternalId) ?? status;
                }

                // External → Sigil: if external was closed and Sigil issue is still open, resolve it
                var changed = link.ExternalStatus != status.Status;
                link.ExternalStatus = status.Status;
                link.LastSyncedAt = dateTime.UtcNow;

                if (changed && status.IsCompleted && link.Issue?.Status == Domain.Enums.IssueStatus.Open)
                {
                    link.Issue.Status = Domain.Enums.IssueStatus.Resolved;
                    dbContext.Issues.Update(link.Issue);
                }

                dbContext.ExternalIssueLinks.Update(link);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync external issue link {LinkId}", link.Id);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
