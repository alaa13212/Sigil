using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.FailedEvents;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.infrastructure.Persistence;

internal class FailedEventService(
    SigilDbContext db,
    IEventIngestionWorker ingestionWorker) : IFailedEventService
{
    public async Task StoreAsync(int projectId, string rawEnvelope, FailedEventStage stage, Exception exception)
    {
        var failedEvent = new FailedEvent
        {
            ProjectId = projectId,
            RawEnvelope = rawEnvelope,
            ErrorMessage = Truncate(exception.Message, 2000),
            ExceptionType = exception.GetType().FullName,
            Stage = stage,
            CreatedAt = DateTime.UtcNow
        };

        db.FailedEvents.Add(failedEvent);
        await db.SaveChangesAsync();
    }

    public async Task<PagedResponse<FailedEventSummary>> GetFailedEventsAsync(int projectId, int page, int pageSize)
    {
        var query = db.FailedEvents
            .Where(f => f.ProjectId == projectId)
            .OrderByDescending(f => f.CreatedAt);

        int totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FailedEventSummary(
                f.Id,
                f.ProjectId,
                f.ErrorMessage,
                f.ExceptionType,
                f.Stage,
                f.CreatedAt,
                f.Reprocessed,
                f.ReprocessedAt))
            .ToListAsync();

        return new PagedResponse<FailedEventSummary>(items, totalCount, page, pageSize);
    }

    public async Task<FailedEvent?> GetByIdAsync(long id)
    {
        return await db.FailedEvents.FindAsync(id);
    }

    public async Task<bool> ReprocessAsync(long id)
    {
        var failedEvent = await db.FailedEvents.FindAsync(id);
        if (failedEvent is null || failedEvent.Reprocessed)
            return false;

        bool enqueued = ingestionWorker.TryEnqueue(
            new IngestionJobItem(failedEvent.ProjectId, failedEvent.RawEnvelope, DateTime.UtcNow));

        if (!enqueued)
            return false;

        db.FailedEvents.Entry(failedEvent).State = EntityState.Modified;
        failedEvent.Reprocessed = true;
        failedEvent.ReprocessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return true;
    }

    public async Task<int> ReprocessAllAsync(int projectId)
    {
        var failedEvents = await db.FailedEvents
            .Where(f => f.ProjectId == projectId && !f.Reprocessed)
            .ToListAsync();

        int count = 0;
        foreach (var failedEvent in failedEvents)
        {
            bool enqueued = ingestionWorker.TryEnqueue(
                new IngestionJobItem(failedEvent.ProjectId, failedEvent.RawEnvelope, DateTime.UtcNow));

            if (!enqueued)
                continue;

            db.FailedEvents.Entry(failedEvent).State = EntityState.Modified;
            failedEvent.Reprocessed = true;
            failedEvent.ReprocessedAt = DateTime.UtcNow;
            count++;
        }

        if (count > 0)
            await db.SaveChangesAsync();

        return count;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var failedEvent = await db.FailedEvents.FindAsync(id);
        if (failedEvent is null)
            return false;

        db.FailedEvents.Remove(failedEvent);
        await db.SaveChangesAsync();
        return true;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
