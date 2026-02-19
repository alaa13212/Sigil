using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Digestion;
using Sigil.infrastructure.Workers;

namespace Sigil.infrastructure.Persistence;

internal class DigestionMonitorService(
    SigilDbContext context,
    IOptions<BatchWorkersConfig> options,
    IRawEnvelopeService rawEnvelopeService,
    IDigestionSignal digestionSignal
    ) : IDigestionMonitorService
{
    public async Task<DigestionStats> GetStatsAsync()
    {
        var groups = await context.RawEnvelopes
            .GroupBy(e => new { e.ProjectId, IsFailed = e.Error != null })
            .Select(g => new { g.Key.ProjectId, g.Key.IsFailed, Count = g.Count() })
            .ToListAsync();

        var oldestPending = await context.RawEnvelopes
            .Where(e => e.Error == null)
            .OrderBy(e => e.ReceivedAt)
            .Select(e => (DateTime?)e.ReceivedAt)
            .FirstOrDefaultAsync();

        var projectIds = groups.Select(g => g.ProjectId).Distinct().ToList();
        var projectNames = await context.Set<Sigil.Domain.Entities.Project>()
            .Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var byProject = groups
            .GroupBy(g => g.ProjectId)
            .Select(pg => new ProjectEnvelopeStats
            {
                ProjectId = pg.Key,
                ProjectName = projectNames.GetValueOrDefault(pg.Key, $"Project {pg.Key}"),
                PendingCount = pg.Where(g => !g.IsFailed).Sum(g => g.Count),
                FailedCount = pg.Where(g => g.IsFailed).Sum(g => g.Count),
            })
            .ToList();

        return new DigestionStats
        {
            PendingCount = groups.Where(g => !g.IsFailed).Sum(g => g.Count),
            FailedCount = groups.Where(g => g.IsFailed).Sum(g => g.Count),
            BatchSize = options.Value.GetOptions(nameof(DigestionWorker)).BatchSize,
            OldestPendingAt = oldestPending,
            ByProject = byProject,
        };
    }

    public async Task<List<FailedEnvelopeSummary>> GetRecentFailuresAsync(int limit = 50)
    {
        var failures = await context.RawEnvelopes
            .Where(e => e.Error != null)
            .OrderByDescending(e => e.ReceivedAt)
            .Take(limit)
            .Join(context.Set<Sigil.Domain.Entities.Project>(),
                e => e.ProjectId,
                p => p.Id,
                (e, p) => new FailedEnvelopeSummary
                {
                    Id = e.Id,
                    ProjectId = e.ProjectId,
                    ProjectName = p.Name,
                    ReceivedAt = e.ReceivedAt,
                    Error = e.Error,
                })
            .ToListAsync();

        return failures;
    }

    public async Task<int> RetryFailedAsync(IEnumerable<long>? ids = null)
    {
        var count = await rawEnvelopeService.RetryFailedAsync(ids);
        if (count > 0)
            digestionSignal.Signal();
        return count;
    }
}
