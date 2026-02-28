using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Shared;

namespace Sigil.Application.Interfaces;

public interface ISharedLinkService
{
    Task<SharedIssueLinkResponse> CreateLinkAsync(int issueId, Guid userId, string hostUrl, TimeSpan? duration = null);
    Task<SharedIssueViewResponse?> ValidateLinkAsync(Guid token);
    Task<PagedResponse<EventSummary>?> GetSharedEventsAsync(Guid token, int page, int pageSize);
    Task<IssueEventDetailResponse?> GetSharedEventDetailAsync(Guid token, long eventId);
    Task<bool> RevokeLinkAsync(Guid token);
}
