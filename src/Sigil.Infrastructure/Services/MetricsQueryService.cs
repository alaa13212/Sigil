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
        DateTime now = dateTime.UtcNow;

        DateTime since5m = now - MetricsDefaults.FiveMinutes;
        DateTime since15m = now - MetricsDefaults.FifteenMinutes;
        DateTime since1h = now - MetricsDefaults.OneHour;
        DateTime since24h = now - MetricsDefaults.OneDay;

        List<IssueCountSample> issues = await dbContext.Issues
            .AsNoTracking()
            .GroupBy(i => new { i.Project!.Name, i.Status, i.Level })
            .Select(g => new IssueCountSample(g.Key.Name, g.Key.Status, g.Key.Level, g.Count()))
            .ToListAsync(ct);

        // One grouped scan produces every window at once: the outer filter bounds the rows
        // and the conditional counts carve the nested windows out of that single pass.
        var grouped = await dbContext.Events
            .AsNoTracking()
            .Where(e => e.Timestamp >= since24h)
            .GroupBy(e => new { e.Project!.Name, e.Level })
            .Select(g => new
            {
                g.Key.Name,
                g.Key.Level,
                Count5m = g.LongCount(e => e.Timestamp >= since5m),
                Count15m = g.LongCount(e => e.Timestamp >= since15m),
                Count1h = g.LongCount(e => e.Timestamp >= since1h),
                Count24h = g.LongCount(),
            })
            .ToListAsync(ct);

        var counts = new Dictionary<(string Project, Severity Level), EventWindowCounts>();
        foreach (var row in grouped)
            counts[(row.Name, row.Level)] = new EventWindowCounts(row.Count5m, row.Count15m, row.Count1h, row.Count24h);

        List<string> projectNames = await dbContext.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(ct);

        List<EventCountSample> events = BuildEventSamples(projectNames, counts);

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
            GetBuildVersion());
    }

    /// <summary>
    /// Emits every project, level and window combination, filling absent combinations with zero
    /// so that a series never disappears when its count reaches zero.
    /// </summary>
    private static List<EventCountSample> BuildEventSamples(
        IReadOnlyList<string> projectNames,
        IReadOnlyDictionary<(string Project, Severity Level), EventWindowCounts> counts)
    {
        Severity[] levels = Enum.GetValues<Severity>();
        var samples = new List<EventCountSample>(projectNames.Count * levels.Length * MetricsDefaults.EventWindows.Count);

        foreach (string project in projectNames)
        {
            foreach (Severity level in levels)
            {
                counts.TryGetValue((project, level), out EventWindowCounts? row);
                row ??= EventWindowCounts.Empty;

                samples.Add(new EventCountSample(project, level, MetricsDefaults.FiveMinutes, row.FiveMinutes));
                samples.Add(new EventCountSample(project, level, MetricsDefaults.FifteenMinutes, row.FifteenMinutes));
                samples.Add(new EventCountSample(project, level, MetricsDefaults.OneHour, row.OneHour));
                samples.Add(new EventCountSample(project, level, MetricsDefaults.OneDay, row.OneDay));
            }
        }

        return samples;
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

    /// <summary>Event counts for one project and level across each reported window.</summary>
    private sealed record EventWindowCounts(long FiveMinutes, long FifteenMinutes, long OneHour, long OneDay)
    {
        /// <summary>Counts for a project and level that recorded no events in the longest window.</summary>
        public static readonly EventWindowCounts Empty = new(0, 0, 0, 0);
    }
}
