using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventService
{
    // Ingestion
    Task<HashSet<string>> FindExistingEventIdsAsync(IEnumerable<string> eventIds);
    IEnumerable<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> capturedEvent, Project project, Issue issue, Dictionary<string, Release> releases, Dictionary<string, EventUser> users, Dictionary<string, TagValue> tagValues);
    Task<bool> SaveEventsAsync(IEnumerable<CapturedEvent> capturedEvents);

    // Entity access (internal use)
    Task<CapturedEvent?> GetEventByIdAsync(long eventId, bool includeStackFrames = false, bool includeTags = false);
    Task<(List<CapturedEvent> Items, int TotalCount)> GetEventsForIssueAsync(int issueId, int page = 1, int pageSize = 50);
    Task<byte[]?> GetRawEventJsonAsync(long eventId);
    Task<string?> GetEventMarkdownAsync(long eventId);

    // DTO access (UI/API)
    Task<PagedResponse<EventSummary>> GetEventSummariesAsync(int issueId, int page = 1, int pageSize = 50);
    Task<EventDetailResponse?> GetEventDetailAsync(long eventId);
    Task<List<BreadcrumbResponse>> GetBreadcrumbsAsync(long eventId);
}
