using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Workers;

internal class RetentionWorker(IServiceProvider services, IAppConfigService appConfig, IProjectConfigService projectConfig, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Stagger startup to avoid competing with digestion
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await EnforceRetentionAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "RetentionWorker encountered an error");
            }

            try
            {
                int intervalMinutes = appConfig.RetentionCheckIntervalMinutes;
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task EnforceRetentionAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

        await CleanFailedEnvelopesAsync(dbContext, ct);

        var projects = await dbContext.Projects.Select(p => p.Id).ToListAsync(ct);
        foreach (var projectId in projects)
        {
            ct.ThrowIfCancellationRequested();
            await EnforceAgeRetentionAsync(dbContext, projectId, ct);
            await EnforceCountRetentionAsync(dbContext, projectId, ct);
        }
    }

    private async Task CleanFailedEnvelopesAsync(SigilDbContext dbContext, CancellationToken ct)
    {
        var failedMaxAgeDays = appConfig.RetentionFailedEnvelopeMaxAgeDays;
        var cutoff = DateTime.UtcNow.AddDays(-failedMaxAgeDays);

        var deleted = await dbContext.RawEnvelopes
            .Where(r => r.Error != null && r.ReceivedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} failed envelopes older than {Days} days", deleted, failedMaxAgeDays);
    }

    private async Task EnforceAgeRetentionAsync(SigilDbContext dbContext, int projectId, CancellationToken ct)
    {
        var maxAgeDays = projectConfig.RetentionMaxAgeDays(projectId) ?? appConfig.RetentionDefaultMaxAgeDays;
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);

        var deleted = await dbContext.Events
            .Where(e => e.ProjectId == projectId && e.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} events older than {Days} days for project {ProjectId}", deleted, maxAgeDays, projectId);
    }

    private async Task EnforceCountRetentionAsync(SigilDbContext dbContext, int projectId, CancellationToken ct)
    {
        var maxEvents = projectConfig.RetentionMaxEventCount(projectId) ?? appConfig.RetentionDefaultMaxEvents;
        var totalEvents = await dbContext.Events.CountAsync(e => e.ProjectId == projectId, ct);
        if (totalEvents <= maxEvents)
            return;

        var excess = totalEvents - maxEvents;
        var oldestIds = await dbContext.Events
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Timestamp)
            .Take(excess)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var deleted = await dbContext.Events
            .Where(e => oldestIds.Contains(e.Id))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} excess events for project {ProjectId} (limit: {Max})", deleted, projectId, maxEvents);
    }
}
