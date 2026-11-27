using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Metrics;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Services;

internal class MetricsQueryService(SigilDbContext dbContext, IDateTime dateTime, ISigilMetrics metrics) : IMetricsQueryService
{
    private const string EnvironmentTagKey = "environment";

    public async Task<MetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        DateTime now = dateTime.UtcNow;

        DateTime since5m = now - MetricsDefaults.FiveMinutes;
        DateTime since15m = now - MetricsDefaults.FifteenMinutes;
        DateTime since1h = now - MetricsDefaults.OneHour;
        DateTime since24h = now - MetricsDefaults.OneDay;

        List<IssueCountSample> issues = await dbContext.Issues
            .AsNoTracking()
            .GroupBy(i => new { i.ProjectId, i.Project!.Name, i.Status, i.Level })
            .Select(g => new IssueCountSample(g.Key.Name, g.Key.ProjectId, g.Key.Status, g.Key.Level, g.Count()))
            .ToListAsync(ct);

        List<EventCountSample> events = await BuildEventSamplesAsync(since5m, since15m, since1h, since24h, ct);

        List<NewIssueCountSample> newIssues = await BuildNewIssueSamplesAsync(since5m, since15m, since1h, since24h, ct);

        List<AlertDeliverySample> deliveries = await dbContext.AlertHistory
            .AsNoTracking()
            .GroupBy(h => new { Type = h.AlertRule!.AlertChannel!.Type, h.Status })
            .Select(g => new AlertDeliverySample(g.Key.Type, g.Key.Status, g.Count()))
            .ToListAsync(ct);

        List<ReleaseInfoSample> releases = await BuildReleaseSamplesAsync(ct);

        List<EnvelopeDiscardSample> discarded = await BuildDiscardedEnvelopeSamplesAsync(ct);

        long backlog = await dbContext.RawEnvelopes
            .AsNoTracking()
            .CountAsync(r => r.Error == null, ct);

        double oldestPending = await GetOldestPendingSecondsAsync(now, ct);

        DateTime? persistedIngestion = await GetPersistedIngestionAsync(ct);

        SigilMetricsSnapshot counters = metrics.GetSnapshot();

        DateTime? lastIngestion = Later(counters.LastIngestionUtc, persistedIngestion);

        return new MetricsSnapshot(
            issues,
            events,
            deliveries,
            newIssues,
            releases,
            discarded,
            counters.EventsIngested,
            counters.EventsDropped,
            backlog,
            oldestPending,
            lastIngestion,
            persistedIngestion,
            GetBuildVersion());
    }

    /// <summary>
    /// Counts events per project, level, window, environment and issue status. Only observed
    /// combinations are reported, the label cross-product is unbounded.
    /// </summary>
    private async Task<List<EventCountSample>> BuildEventSamplesAsync(
        DateTime since5m,
        DateTime since15m,
        DateTime since1h,
        DateTime since24h,
        CancellationToken ct)
    {
        var environmentByEvent = from tag in dbContext.EventTags.AsNoTracking()
                                  join value in dbContext.TagValues.AsNoTracking() on tag.TagValueId equals value.Id
                                  join key in dbContext.TagKeys.AsNoTracking() on value.TagKeyId equals key.Id
                                  where key.Key == EnvironmentTagKey
                                  select new { tag.EventId, Environment = value.Value };

        // One grouped scan produces every window at once: the outer filter bounds the rows
        // and the conditional counts carve the nested windows out of that single pass.
        List<EventGroupRow> grouped = await (
            from e in dbContext.Events.AsNoTracking()
            where e.Timestamp >= since24h
            join env in environmentByEvent on e.Id equals env.EventId into environments
            from env in environments.DefaultIfEmpty()
            group new { e.Timestamp } by new
            {
                e.ProjectId,
                Name = e.Project!.Name,
                e.Level,
                Status = e.Issue!.Status,
                Environment = env.Environment,
            } into g
            select new EventGroupRow(
                g.Key.ProjectId,
                g.Key.Name,
                g.Key.Level,
                g.Key.Status,
                g.Key.Environment,
                g.LongCount(x => x.Timestamp >= since5m),
                g.LongCount(x => x.Timestamp >= since15m),
                g.LongCount(x => x.Timestamp >= since1h),
                g.LongCount())).ToListAsync(ct);

        Dictionary<int, HashSet<string>> allowedEnvironments = AllowedEnvironments(grouped);

        var samples = new List<EventCountSample>();
        foreach (EventGroupRow row in grouped
                     .OrderBy(r => r.Name)
                     .ThenBy(r => r.Level)
                     .ThenBy(r => r.Environment, StringComparer.Ordinal)
                     .ThenBy(r => r.Status))
        {
            string environment = EnvironmentLabel(row.ProjectId, row.Environment, allowedEnvironments);

            foreach ((TimeSpan window, long count) in Windows(row.Count5m, row.Count15m, row.Count1h, row.Count24h))
            {
                if (count <= 0)
                    continue;

                samples.Add(new EventCountSample(row.Name, row.ProjectId, row.Level, window, environment, row.Status, count));
            }
        }

        return samples;
    }

    /// <summary>Counts issues first seen inside each window, grouped by project and level.</summary>
    private async Task<List<NewIssueCountSample>> BuildNewIssueSamplesAsync(
        DateTime since5m,
        DateTime since15m,
        DateTime since1h,
        DateTime since24h,
        CancellationToken ct)
    {
        List<NewIssueGroupRow> grouped = await dbContext.Issues
            .AsNoTracking()
            .Where(i => i.FirstSeen >= since24h)
            .GroupBy(i => new { i.ProjectId, i.Project!.Name, i.Level })
            .Select(g => new NewIssueGroupRow(
                g.Key.ProjectId,
                g.Key.Name,
                g.Key.Level,
                g.LongCount(i => i.FirstSeen >= since5m),
                g.LongCount(i => i.FirstSeen >= since15m),
                g.LongCount(i => i.FirstSeen >= since1h),
                g.LongCount()))
            .ToListAsync(ct);

        var samples = new List<NewIssueCountSample>();
        foreach (NewIssueGroupRow row in grouped.OrderBy(r => r.Name).ThenBy(r => r.Level))
        {
            foreach ((TimeSpan window, long count) in Windows(row.Count5m, row.Count15m, row.Count1h, row.Count24h))
            {
                if (count <= 0)
                    continue;

                samples.Add(new NewIssueCountSample(row.Name, row.ProjectId, row.Level, window, count));
            }
        }

        return samples;
    }

    /// <summary>Newest release observed for every project that has one.</summary>
    private async Task<List<ReleaseInfoSample>> BuildReleaseSamplesAsync(CancellationToken ct)
    {
        Dictionary<int, string> projectNames = await dbContext.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var releases = await dbContext.Releases
            .AsNoTracking()
            .Select(r => new { r.Id, r.ProjectId, r.RawName, r.CommitSha, r.FirstSeenAt })
            .OrderByDescending(r => r.FirstSeenAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var samples = new List<ReleaseInfoSample>();
        var seenProjects = new HashSet<int>();

        foreach (var release in releases)
        {
            if (!seenProjects.Add(release.ProjectId))
                continue;

            samples.Add(new ReleaseInfoSample(
                projectNames.GetValueOrDefault(release.ProjectId, $"Project {release.ProjectId}"),
                release.ProjectId,
                release.RawName,
                release.CommitSha,
                release.FirstSeenAt));
        }

        return samples;
    }

    /// <summary>Counts the envelopes discarded for an item type Sigil does not process.</summary>
    private async Task<List<EnvelopeDiscardSample>> BuildDiscardedEnvelopeSamplesAsync(CancellationToken ct)
    {
        var grouped = await dbContext.RawEnvelopes
            .AsNoTracking()
            .Where(r => r.Error != null && r.Error.StartsWith(EnvelopeItemTypes.UnsupportedErrorPrefix))
            .GroupBy(r => r.Error!)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in grouped)
        {
            string type = EnvelopeItemTypes.Normalize(row.Key[EnvelopeItemTypes.UnsupportedErrorPrefix.Length..]);
            counts[type] = counts.GetValueOrDefault(type) + row.Count;
        }

        return counts
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => new EnvelopeDiscardSample(c.Key, c.Value))
            .ToList();
    }

    /// <summary>Age of the oldest envelope still awaiting digestion.</summary>
    private async Task<double> GetOldestPendingSecondsAsync(DateTime now, CancellationToken ct)
    {
        DateTime? oldest = await dbContext.RawEnvelopes
            .AsNoTracking()
            .Where(r => r.Error == null)
            .OrderBy(r => r.ReceivedAt)
            .Select(r => (DateTime?)r.ReceivedAt)
            .FirstOrDefaultAsync(ct);

        if (oldest is null)
            return 0;

        double seconds = (now - oldest.Value).TotalSeconds;
        return seconds > 0 ? seconds : 0;
    }

    /// <summary>Most recent ingestion instant found in persisted data.</summary>
    private async Task<DateTime?> GetPersistedIngestionAsync(CancellationToken ct)
    {
        DateTime? envelopeReceivedAt = await dbContext.RawEnvelopes
            .AsNoTracking()
            .OrderByDescending(r => r.ReceivedAt)
            .Select(r => (DateTime?)r.ReceivedAt)
            .FirstOrDefaultAsync(ct);

        DateTime? eventReceivedAt = await dbContext.Events
            .AsNoTracking()
            .OrderByDescending(e => e.ReceivedAt)
            .Select(e => (DateTime?)e.ReceivedAt)
            .FirstOrDefaultAsync(ct);

        return Later(envelopeReceivedAt, eventReceivedAt);
    }

    /// <summary>Most frequent environment values per project, capped at the label limit.</summary>
    private static Dictionary<int, HashSet<string>> AllowedEnvironments(IEnumerable<EventGroupRow> grouped)
    {
        var frequency = new Dictionary<(int ProjectId, string Environment), long>();

        foreach (EventGroupRow row in grouped)
        {
            if (string.IsNullOrEmpty(row.Environment))
                continue;

            var key = (row.ProjectId, row.Environment);
            frequency[key] = frequency.GetValueOrDefault(key) + row.Count24h;
        }

        var allowed = new Dictionary<int, HashSet<string>>();
        foreach (var project in frequency.GroupBy(f => f.Key.ProjectId))
        {
            var values = project
                .OrderByDescending(f => f.Value)
                .ThenBy(f => f.Key.Environment, StringComparer.Ordinal)
                .Take(MetricsDefaults.EnvironmentLabelLimit)
                .Select(f => f.Key.Environment)
                .ToHashSet(StringComparer.Ordinal);

            allowed[project.Key] = values;
        }

        return allowed;
    }

    /// <summary>Label of one environment value: its own name, the folded label or the missing label.</summary>
    private static string EnvironmentLabel(
        int projectId,
        string? environment,
        IReadOnlyDictionary<int, HashSet<string>> allowedEnvironments)
    {
        if (string.IsNullOrEmpty(environment))
            return MetricsDefaults.EnvironmentMissingLabel;

        if (allowedEnvironments.TryGetValue(projectId, out HashSet<string>? values) && values.Contains(environment))
            return environment;

        return MetricsDefaults.EnvironmentOtherLabel;
    }

    /// <summary>The reported windows with their counts.</summary>
    private static (TimeSpan Window, long Count)[] Windows(long count5m, long count15m, long count1h, long count24h) =>
    [
        (MetricsDefaults.FiveMinutes, count5m),
        (MetricsDefaults.FifteenMinutes, count15m),
        (MetricsDefaults.OneHour, count1h),
        (MetricsDefaults.OneDay, count24h),
    ];

    /// <summary>Later of two instants.</summary>
    private static DateTime? Later(DateTime? first, DateTime? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        return first >= second ? first : second;
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

    /// <summary>Aggregated event counts for one project, level, environment and issue status.</summary>
    private sealed record EventGroupRow(
        int ProjectId,
        string Name,
        Severity Level,
        IssueStatus Status,
        string? Environment,
        long Count5m,
        long Count15m,
        long Count1h,
        long Count24h);

    /// <summary>Aggregated issue counts for one project and level.</summary>
    private sealed record NewIssueGroupRow(
        int ProjectId,
        string Name,
        Severity Level,
        long Count5m,
        long Count15m,
        long Count1h,
        long Count24h);
}
