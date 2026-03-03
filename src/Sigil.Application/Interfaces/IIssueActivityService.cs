using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IIssueActivityService
{
    Task<(List<IssueActivity> Items, int TotalCount)> GetActivitiesForIssueAsync(int issueId, int page = 1, int pageSize = 50);
    Task<PagedResponse<ActivityResponse>> GetActivitySummariesAsync(int issueId, int page = 1, int pageSize = 50);
    Task<ActivityResponse> AddCommentAsync(int issueId, string message);
}
