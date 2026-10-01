using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Metrics;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Services;

internal class MetricsQueryService(SigilDbContext dbContext, IDateTime dateTime, ISigilMetrics metrics) : IMetricsQueryService
{
    public async Task<MetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var since = dateTime.UtcNow - MetricsDefaults.EventWindow;

        List<IssueCountSample> issues = await dbContext.Issues
            .AsNoTracking()
            .GroupBy(i => new { i.Project!.Name, i.Status, i.Level })
            .Select(g => new IssueCountSample(g.Key.Name, g.Key.Status, g.Key.Level, g.Count()))
            .ToListAsync(ct);

        List<EventCountSample> events = await dbContext.Events
            .AsNoTracking()
            .Where(e => e.Timestamp >= since)
            .GroupBy(e => new { e.Project!.Name, e.Level })
            .Select(g => new EventCountSample(g.Key.Name, g.Key.Level, g.Count()))
            .ToListAsync(ct);

        List<AlertDeliverySample> deliveries = await dbContext.AlertHistory
            .AsNoTracking()
            .GroupBy(h => new { Type = h.AlertRule!.AlertChannel!.Type, h.Status })
            .Select(g => new AlertDeliverySample(g.Key.Type, g.Key.Status, g.Count()))
            .ToListAsync(ct);

        long backlog = await dbContext.RawEnvelopes
            .AsNoTracking()
            .CountAsync(r => r.Error == null, ct);

        SigilMetricsSnapshot counters = metrics.GetSnapshot();

        return new MetricsSnapshot(
            issues,
            events,
            deliveries,
            counters.EventsIngested,
            counters.EventsDropped,
            backlog,
            counters.LastIngestionUtc,
            GetBuildVersion(),
            MetricsDefaults.EventWindow);
    }

    /// <summary>Version of the running build, from assembly metadata.</summary>
    public static string GetBuildVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(MetricsQueryService).Assembly;
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
