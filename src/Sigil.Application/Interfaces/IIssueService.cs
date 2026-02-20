using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IIssueService
{
    // Ingestion
    Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint);

    // Entity access (internal use)
    Task<Issue?> GetIssueByIdAsync(int issueId, bool includeTags = false, bool includeEvents = false);
    Task<(List<Issue> Items, int TotalCount)> GetIssuesAsync(int projectId, IssueQueryParams query);

    // DTO access (UI/API)
    Task<PagedResponse<IssueSummary>> GetIssueSummariesAsync(int projectId, IssueQueryParams query);
    Task<IssueDetailResponse?> GetIssueDetailAsync(int issueId);

    // Mutations
    Task<Issue> UpdateIssueStatusAsync(int issueId, IssueStatus status, Guid? userId = null, bool ignoreFutureEvents = false);
    Task<Issue> AssignIssueAsync(int issueId, Guid? assignToUserId, Guid? actionByUserId = null);
    Task<Issue> UpdateIssuePriorityAsync(int issueId, Priority priority);
    Task<bool> DeleteIssueAsync(int issueId);

    // Discovery
    Task<List<IssueSummary>> GetSimilarIssuesAsync(int issueId);
}
