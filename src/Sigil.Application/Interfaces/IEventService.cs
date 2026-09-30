using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IEventService
{
    // Entity access (internal use)
    Task<CapturedEvent?> GetEventByIdAsync(long eventId, bool includeStackFrames = false, bool includeTags = false);
    Task<(List<CapturedEvent> Items, int TotalCount)> GetEventsForIssueAsync(int issueId, EventQueryParams query);
    Task<byte[]?> GetRawEventJsonAsync(long eventId);
    Task<string?> GetEventMarkdownAsync(long eventId);

    // DTO access (UI/API)
    Task<PagedResponse<EventSummary>> GetEventSummariesAsync(int issueId, EventQueryParams query);
    Task<PagedResponse<EventSummary>> SearchEventsAsync(int projectId, EventQueryParams query);
    Task<EventDetailResponse?> GetEventDetailAsync(long eventId);
    Task<IssueEventDetailResponse?> GetIssueEventDetailAsync(int issueId, long eventId);
    Task<List<BreadcrumbResponse>> GetBreadcrumbsAsync(long eventId);
    Task<EventNavigationResponse> GetAdjacentEventIdsAsync(int issueId, long currentEventId);
    Task<EventNavigationResponse> GetMergeGroupEventNavigationAsync(int mergeSetId, long currentEventId);
}
