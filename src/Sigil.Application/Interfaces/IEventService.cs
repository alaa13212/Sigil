using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventService
{
    // Ingestion
    Task<HashSet<string>> FindExistingEventIdsAsync(IEnumerable<string> eventIds);
    List<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> parsedEvents, Project project, Issue issue,
        Dictionary<string, Release> releases, Dictionary<string, EventUser> users,
        Dictionary<string, Dictionary<string, int>> tagValues);
    Task<bool> SaveEventsAsync();

    // Entity access (internal use)
    Task<CapturedEvent?> GetEventByIdAsync(long eventId, bool includeStackFrames = false, bool includeTags = false);
    Task<(List<CapturedEvent> Items, int TotalCount)> GetEventsForIssueAsync(int issueId, int page = 1, int pageSize = 50);
    Task<byte[]?> GetRawEventJsonAsync(long eventId);
    Task<string?> GetEventMarkdownAsync(long eventId);

    // DTO access (UI/API)
    Task<PagedResponse<EventSummary>> GetEventSummariesAsync(int issueId, int page = 1, int pageSize = 50);
    Task<EventDetailResponse?> GetEventDetailAsync(long eventId);
    Task<IssueEventDetailResponse?> GetIssueEventDetailAsync(int issueId, long eventId);
    Task<List<BreadcrumbResponse>> GetBreadcrumbsAsync(long eventId);
    Task<EventNavigationResponse> GetAdjacentEventIdsAsync(int issueId, long currentEventId);
}
