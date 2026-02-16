using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;

namespace Sigil.infrastructure.Persistence;

internal class EventService(SigilDbContext dbContext, ICompressionService compressionService, IDateTime dateTime) : IEventService
{
    public IEnumerable<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> capturedEvent, Issue issue,
        Dictionary<string, Release> releases, Dictionary<string, EventUser> users,
        Dictionary<string, TagValue> tagValues)
    {
        return capturedEvent.Select(e => new CapturedEvent
        {
            EventId = e.EventId,
            Timestamp = e.Timestamp,
            ReceivedAt = dateTime.UtcNow,
            Message = e.Message,
            Level = e.Level,
            Logger = e.Logger,
            Platform = e.Platform,
            ReleaseId = releases[e.Release].Id,
            Extra = e.Extra,
            Issue = issue,
            UserId = e.User != null? users[e.User.UniqueIdentifier!].UniqueIdentifier : null,
            StackFrames = MakeStackFrames(e),
            Tags = MakeTags(e.Tags, tagValues),
            RawCompressedJson = compressionService.CompressString(e.RawJson),

        });
    }

    public async Task<bool> SaveEventsAsync(IEnumerable<CapturedEvent> capturedEvents)
    {
        dbContext.Events.AddRange(capturedEvents);
        return await dbContext.SaveChangesAsync() > 0;
    }

    private static ICollection<StackFrame> MakeStackFrames(ParsedEvent parsedEvent)
    {
        return parsedEvent.Stacktrace.Select(frame => new StackFrame
        {
            Filename = frame.Filename,
            Function = frame.Function,
            Module = frame.Module,
            LineNumber = frame.LineNumber,
            ColumnNumber = frame.ColumnNumber,
            InApp = frame.InApp,
        }).ToList();
    }

    private static ICollection<TagValue> MakeTags(Dictionary<string, string>? eventTags, Dictionary<string, TagValue> tagValues)
    {
        if (eventTags.IsNullOrEmpty())
            return [];
        
        return eventTags.Select(tag => tagValues[$"{tag.Key}:{tag.Value}"]).ToList();
    }
}
