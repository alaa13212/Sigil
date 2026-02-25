using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Domain.Entities;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Persistence;

internal class EventService(SigilDbContext dbContext, ICompressionService compressionService) : IEventService
{
    public async Task<HashSet<string>> FindExistingEventIdsAsync(IEnumerable<string> eventIds)
    {
        var ids = eventIds.ToList();
        var existing = await dbContext.Events
            .Where(e => ids.Contains(e.EventId))
            .Select(e => e.EventId)
            .ToHashSetAsync();
        return existing;
    }

    public List<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> parsedEvents, Project project,
        Issue issue,
        Dictionary<string, Release> releases, Dictionary<string, EventUser> users,
        Dictionary<string, Dictionary<string, int>> tagValues)
    {
        List<CapturedEvent> capturedEvents = new List<CapturedEvent>();
        List<EventTag> eventTags = new List<EventTag>();
        foreach (var parsedEvent in parsedEvents)
        {
            var capturedEvent = new CapturedEvent
            {
                EventId = parsedEvent.EventId,
                Timestamp = parsedEvent.Timestamp,
                ReceivedAt = parsedEvent.ReceivedAt,
                ProcessedAt = DateTime.UtcNow,
                Message = parsedEvent.Message,
                Level = parsedEvent.Level,
                Logger = parsedEvent.Logger,
                Platform = parsedEvent.Platform,
                ReleaseId = parsedEvent.Release != null ? releases[parsedEvent.Release].Id : null,
                Extra = parsedEvent.Extra,
                ProjectId = project.Id,
                Issue = issue,
                UserId = parsedEvent.User != null ? users[parsedEvent.User.UniqueIdentifier!].UniqueIdentifier : null,
                StackFrames = MakeStackFrames(parsedEvent),
                RawCompressedJson = compressionService.CompressString(parsedEvent.RawJson),
            };
            
            capturedEvents.Add(capturedEvent);
            eventTags.AddRange(MakeTags(capturedEvent, parsedEvent.Tags, tagValues));                                                                                             
        }

        dbContext.Events.AddRange(capturedEvents);
        dbContext.EventTags.AddRange(eventTags);
        return capturedEvents;
    }

    public async Task<bool> SaveEventsAsync()
    {
        return await dbContext.SaveChangesAsync() > 0;
    }

    public async Task<CapturedEvent?> GetEventByIdAsync(long eventId, bool includeStackFrames = false, bool includeTags = false)
    {
        IQueryable<CapturedEvent> query = dbContext.Events;

        if (includeStackFrames)
            query = query.Include(e => e.StackFrames);

        if (includeTags)
            query = query.Include(e => e.Tags).ThenInclude(tv => tv.TagKey);

        return await query
            .Include(e => e.User)
            .Include(e => e.Release)
            .FirstOrDefaultAsync(e => e.Id == eventId);
    }

    public async Task<(List<CapturedEvent> Items, int TotalCount)> GetEventsForIssueAsync(int issueId, int page = 1, int pageSize = 50)
    {
        var query = dbContext.Events.Where(e => e.IssueId == issueId);

        int totalCount = await query.CountAsync();

        var items = await query
            .Include(e => e.User)
            .Include(e => e.Release)
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<byte[]?> GetRawEventJsonAsync(long eventId)
    {
        var compressed = await dbContext.Events
            .Where(e => e.Id == eventId)
            .Select(e => e.RawCompressedJson)
            .FirstOrDefaultAsync();

        if (compressed is null)
            return null;

        var json = compressionService.DecompressToString(compressed);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public async Task<string?> GetEventMarkdownAsync(long eventId)                                                                                                               
    {                                                                                                                                                                            
        var evt = await GetEventDetailAsync(eventId);                                                                                                                            
        if (evt is null) return null;                                                                                                                                            
                                                                                                                                                                                 
        var sb = new StringBuilder();                                                                                                                                            
                                                                                                                                                                                 
        sb.AppendLine($"# Event {evt.Id}");                                                                                                                                      
        sb.AppendLine();                                                                                                                                                         
        sb.AppendLine($"**Level:** {evt.Level}  ");                                                                                                                              
        sb.AppendLine($"**Timestamp:** {evt.Timestamp:yyyy-MM-dd HH:mm:ss} UTC  ");                                                                                              
        sb.AppendLine($"**Platform:** {evt.Platform}  ");                                                                                                                        
        if (!string.IsNullOrEmpty(evt.Release))                                                                                                                                  
            sb.AppendLine($"**Release:** {evt.Release}  ");                                                                                                                      
        if (!string.IsNullOrEmpty(evt.Environment))                                                                                                                              
            sb.AppendLine($"**Environment:** {evt.Environment}  ");                                                                                                              
        if (!string.IsNullOrEmpty(evt.EventId))                                                                                                                                  
            sb.AppendLine($"**Event ID:** `{evt.EventId}`  ");                                                                                                                   
                                                                                                                                                                                 
        if (!string.IsNullOrEmpty(evt.Message))                                                                                                                                  
        {                                                                                                                                                                        
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine("## Exception");                                                                                                                                       
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine($"```");                                                                                                                                               
            sb.AppendLine(evt.Message);                                                                                                                                          
            sb.AppendLine("```");                                                                                                                                                
        }                                                                                                                                                                        
                                                                                                                                                                                 
        if (evt.StackFrames.Count > 0)                                                                                                                                           
        {                                                                                                                                                                        
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine("## Stack Trace");                                                                                                                                     
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine("```");                                                                                                                                                
            foreach (var frame in evt.StackFrames)                                                                                                                               
            {                                                                                                                                                                    
                var location = frame.Filename is not null                                                                                                                        
                    ? $" in {frame.Filename}{(frame.LineNumber.HasValue ? $":{frame.LineNumber}" : "")}"                                                                         
                    : "";                                                                                                                                                        
                var inApp = frame.InApp ? "" : " [external]";                                                                                                                    
                sb.AppendLine($"  at {frame.Function ?? "?"}{location}{inApp}");                                                                                                 
            }                                                                                                                                                                    
            sb.AppendLine("```");                                                                                                                                                
        }                                                                                                                                                                        
                                                                                                                                                                                 
        if (evt.User is not null)                                                                                                                                                
        {                                                                                                                                                                        
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine("## User");                                                                                                                                            
            sb.AppendLine();                                                                                                                                                     
            if (!string.IsNullOrEmpty(evt.User.Identifier)) sb.AppendLine($"- **ID:** {evt.User.Identifier}");                                                                   
            if (!string.IsNullOrEmpty(evt.User.Username))   sb.AppendLine($"- **Username:** {evt.User.Username}");                                                               
            if (!string.IsNullOrEmpty(evt.User.Email))      sb.AppendLine($"- **Email:** {evt.User.Email}");                                                                     
            if (!string.IsNullOrEmpty(evt.User.IpAddress))  sb.AppendLine($"- **IP:** {evt.User.IpAddress}");                                                                    
        }                                                                                                                                                                        
                                                                                                                                                                                 
        var displayTags = evt.Tags.Where(t => t.Key != "environment").ToList();                                                                                                  
        if (displayTags.Count > 0)                                                                                                                                               
        {                                                                                                                                                                        
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine("## Tags");                                                                                                                                            
            sb.AppendLine();                                                                                                                                                     
            sb.AppendLine("| Key | Value |");                                                                                                                                    
            sb.AppendLine("|-----|-------|");                                                                                                                                    
            foreach (var tag in displayTags)                                                                                                                                     
                sb.AppendLine($"| {tag.Key} | {tag.Value} |");                                                                                                                   
        }                                                                                                                                                                        
                                                                                                                                                                                 
        return sb.ToString();                                                                                                                                                    
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

    private static IEnumerable<EventTag> MakeTags(CapturedEvent capturedEvent, Dictionary<string, string>? eventTags, Dictionary<string, Dictionary<string, int>> tagValues)
    {
        if (eventTags.IsNullOrEmpty())
            return [];

        return eventTags.Select(tag => new EventTag
        {
            Event = capturedEvent,
            TagValueId = tagValues[tag.Key][tag.Value]
        });
    }

    public async Task<PagedResponse<EventSummary>> GetEventSummariesAsync(int issueId, int page = 1, int pageSize = 50)
    {
        var (items, totalCount) = await GetEventsForIssueAsync(issueId, page, pageSize);

        var summaries = items.Select(e => new EventSummary(
            e.Id, e.EventId, e.Message, e.Level,
            e.Timestamp, e.Release?.RawName, e.User?.Identifier)).ToList();

        return new PagedResponse<EventSummary>(summaries, totalCount, page, pageSize);
    }

    public async Task<EventDetailResponse?> GetEventDetailAsync(long eventId)
    {
        var e = await GetEventByIdAsync(eventId, includeStackFrames: true, includeTags: true);
        if (e is null) return null;

        var tags = e.Tags
            .Where(tv => tv.TagKey != null)
            .Select(tv => new TagSummary(tv.TagKey!.Key, tv.Value))
            .ToList();
        
        var stackFrames = e.StackFrames
            .Select(f => new StackFrameResponse(f.Function, f.Filename, f.LineNumber, f.ColumnNumber, f.Module, f.InApp))
            .ToList();

        EventUserResponse? user = e.User is not null
            ? new EventUserResponse(e.User.Username, e.User.Email, e.User.IpAddress, e.User.Identifier)
            : null;

        var environment = tags.FirstOrDefault(t => t.Key == "environment")?.Value;

        return new EventDetailResponse(
            e.Id, e.EventId, e.IssueId, e.Message, e.Level,
            e.Timestamp, e.Platform, e.Release?.RawName,
            environment, user, stackFrames, tags, e.Extra);
    }

    public async Task<IssueEventDetailResponse?> GetIssueEventDetailAsync(int issueId, long eventId)
    {
        var detail = await GetEventDetailAsync(eventId);
        if (detail is null) return null;

        var breadcrumbs = await GetBreadcrumbsAsync(eventId);
        var navigation = await GetAdjacentEventIdsAsync(issueId, eventId);

        return new IssueEventDetailResponse(detail, breadcrumbs, navigation);
    }

    public async Task<EventNavigationResponse> GetAdjacentEventIdsAsync(int issueId, long currentEventId)
    {
        var currentTimestamp = await dbContext.Events
            .Where(e => e.Id == currentEventId)
            .Select(e => e.Timestamp)
            .FirstOrDefaultAsync();

        // Previous = newer event (ordered by timestamp desc, the one before current)
        var nextId = await dbContext.Events
            .Where(e => e.IssueId == issueId && (e.Timestamp > currentTimestamp || (e.Timestamp == currentTimestamp && e.Id > currentEventId)))
            .OrderBy(e => e.Timestamp).ThenBy(e => e.Id)
            .Select(e => (long?)e.Id)
            .FirstOrDefaultAsync();

        // Next = older event (ordered by timestamp desc, the one after current)
        var previousId = await dbContext.Events
            .Where(e => e.IssueId == issueId && (e.Timestamp < currentTimestamp || (e.Timestamp == currentTimestamp && e.Id < currentEventId)))
            .OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id)
            .Select(e => (long?)e.Id)
            .FirstOrDefaultAsync();

        return new EventNavigationResponse(previousId, nextId);
    }

    public async Task<List<BreadcrumbResponse>> GetBreadcrumbsAsync(long eventId)
    {
        var rawBytes = await GetRawEventJsonAsync(eventId);
        if (rawBytes is null) return [];

        try
        {
            using var doc = JsonDocument.Parse(rawBytes);
            var root = doc.RootElement;

            if (!root.TryGetProperty("breadcrumbs", out var breadcrumbsEl))
                return [];

            JsonElement valuesArray;
            if (breadcrumbsEl.ValueKind == JsonValueKind.Object &&
                breadcrumbsEl.TryGetProperty("values", out var vals))
                valuesArray = vals;
            else if (breadcrumbsEl.ValueKind == JsonValueKind.Array)
                valuesArray = breadcrumbsEl;
            else
                return [];

            var breadcrumbs = new List<BreadcrumbResponse>();
            foreach (var item in valuesArray.EnumerateArray())
            {
                DateTime? timestamp = null;
                if (item.TryGetProperty("timestamp", out var ts))
                {
                    if (ts.ValueKind == JsonValueKind.Number)
                        timestamp = DateTimeOffset.FromUnixTimeSeconds((long)ts.GetDouble()).UtcDateTime;
                    else if (ts.ValueKind == JsonValueKind.String && DateTime.TryParse(ts.GetString(), out var parsed))
                        timestamp = parsed.ToUniversalTime();
                }

                Dictionary<string, object>? data = null;
                if (item.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                {
                    data = new Dictionary<string, object>();
                    foreach (var prop in dataEl.EnumerateObject())
                    {
                        data[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString()!,
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }

                breadcrumbs.Add(new BreadcrumbResponse(
                    timestamp,
                    item.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    item.TryGetProperty("message", out var msg) ? msg.GetString() : null,
                    item.TryGetProperty("level", out var lvl) ? lvl.GetString() : null,
                    item.TryGetProperty("type", out var typ) ? typ.GetString() : null,
                    data));
            }

            breadcrumbs.Reverse();
            return breadcrumbs;
        }
        catch
        {
            return [];
        }
    }
}
