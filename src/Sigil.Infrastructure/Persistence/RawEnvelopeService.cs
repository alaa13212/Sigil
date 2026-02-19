using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence;

internal class RawEnvelopeService(SigilDbContext context) : IRawEnvelopeService
{
    public async Task StoreAsync(int projectId, string rawData, DateTime receivedAt)
    {
        context.RawEnvelopes.Add(new RawEnvelope { ProjectId = projectId, RawData = rawData, ReceivedAt = receivedAt });
        await context.SaveChangesAsync();
    }

    public async Task BulkStoreAsync(IEnumerable<(int ProjectId, string RawData, DateTime ReceivedAt)> items)
    {
        var entities = items.Select(i => new RawEnvelope
        {
            ProjectId = i.ProjectId,
            RawData = i.RawData,
            ReceivedAt = i.ReceivedAt
        });
        context.RawEnvelopes.AddRange(entities);
        await context.SaveChangesAsync();
    }

    public async Task<List<RawEnvelope>> FetchUnprocessedAsync(int batchSize)
    {
        return await context.RawEnvelopes
            .AsTracking()
            .Where(e => e.Error == null)
            .OrderBy(e => e.ReceivedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task DeleteAsync(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        await context.RawEnvelopes
            .Where(e => idList.Contains(e.Id))
            .ExecuteDeleteAsync();
    }

    public async Task<int> RetryFailedAsync(IEnumerable<long>? ids = null)
    {
        var idList = ids?.ToList();
        var query = context.RawEnvelopes.Where(e => e.Error != null);

        if (idList is { Count: > 0 })
            query = query.Where(e => idList.Contains(e.Id));

        return await query.ExecuteUpdateAsync(s => s
            .SetProperty(e => e.Error, (string?)null)
            .SetProperty(e => e.ProcessedAt, (DateTime?)null));
    }

    public async Task BulkMarkFailedAsync(IEnumerable<(long Id, string Error)> failures)
    {
        var failureList = failures.ToList();
        if (failureList.Count == 0) return;

        var ids = failureList.Select(f => f.Id).ToList();
        var errorById = failureList.ToDictionary(f => f.Id, f => f.Error);

        var entities = await context.RawEnvelopes
            .Where(e => ids.Contains(e.Id))
            .AsTracking()
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.ProcessedAt = now;
            entity.Error = errorById[entity.Id];
        }

        await context.SaveChangesAsync();
    }
}
