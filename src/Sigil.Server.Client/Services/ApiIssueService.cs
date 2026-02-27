using System.Net.Http.Json;
using System.Web;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Server.Client.Services;

public class ApiIssueService(HttpClient http) : IIssueService
{
    public async Task<PagedResponse<IssueSummary>> GetIssueSummariesAsync(int projectId, IssueQueryParams query)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        if (query.Status.HasValue) qs["status"] = query.Status.Value.ToString();
        if (query.Priority.HasValue) qs["priority"] = query.Priority.Value.ToString();
        if (query.Level.HasValue) qs["level"] = query.Level.Value.ToString();
        if (!string.IsNullOrWhiteSpace(query.Search)) qs["search"] = query.Search;
        if (query.AssignedToId.HasValue) qs["assignedToId"] = query.AssignedToId.Value.ToString();
        qs["sortBy"] = query.SortBy.ToString();
        qs["sortDesc"] = query.SortDescending.ToString().ToLower();
        qs["page"] = query.Page.ToString();
        qs["pageSize"] = query.PageSize.ToString();
        if (query.Bookmarked) qs["bookmarked"] = "true";
        qs["includeViewedInfo"] = "true";

        return await http.GetFromJsonAsync<PagedResponse<IssueSummary>>(
            $"api/projects/{projectId}/issues?{qs}") ?? new PagedResponse<IssueSummary>([], 0, 1, query.PageSize);
    }

    public async Task<IssueDetailResponse?> GetIssueDetailAsync(int issueId)
    {
        try
        {
            return await http.GetFromJsonAsync<IssueDetailResponse>($"api/issues/{issueId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<(List<Issue> Items, int TotalCount)> GetIssuesAsync(int projectId, IssueQueryParams query)
    {
        var response = await GetIssueSummariesAsync(projectId, query);
        var issues = response.Items.Select(s => new Issue
        {
            Id = s.Id, Title = s.Title, ExceptionType = s.ExceptionType, Culprit = s.Culprit,
            Status = s.Status, Priority = s.Priority, Level = s.Level,
            FirstSeen = s.FirstSeen, LastSeen = s.LastSeen, OccurrenceCount = s.OccurrenceCount,
            Fingerprint = "", ProjectId = projectId,
        }).ToList();
        return (issues, response.TotalCount);
    }

    public async Task<Issue?> GetIssueByIdAsync(int issueId, bool includeTags = false, bool includeEvents = false)
    {
        var detail = await GetIssueDetailAsync(issueId);
        if (detail is null) return null;
        return new Issue
        {
            Id = detail.Id, Title = detail.Title, ExceptionType = detail.ExceptionType, Culprit = detail.Culprit,
            Fingerprint = detail.Fingerprint, Status = detail.Status, Priority = detail.Priority, Level = detail.Level,
            FirstSeen = detail.FirstSeen, LastSeen = detail.LastSeen, OccurrenceCount = detail.OccurrenceCount,
            AssignedToId = detail.AssignedToId, ResolvedAt = detail.ResolvedAt, ProjectId = 0,
        };
    }

    public async Task<Issue> UpdateIssueStatusAsync(int issueId, IssueStatus status, Guid? userId = null, bool ignoreFutureEvents = false)
    {
        await http.PutAsJsonAsync($"api/issues/{issueId}/status", new UpdateStatusRequest(status, ignoreFutureEvents));
        return (await GetIssueByIdAsync(issueId))!;
    }

    public async Task<Issue> AssignIssueAsync(int issueId, Guid? assignToUserId, Guid? actionByUserId = null)
    {
        await http.PutAsJsonAsync($"api/issues/{issueId}/assign", new AssignRequest(assignToUserId));
        return (await GetIssueByIdAsync(issueId))!;
    }

    public async Task<Issue> UpdateIssuePriorityAsync(int issueId, Priority priority)
    {
        await http.PutAsJsonAsync($"api/issues/{issueId}/priority", new UpdatePriorityRequest(priority));
        return (await GetIssueByIdAsync(issueId))!;
    }

    public async Task<bool> DeleteIssueAsync(int issueId)
    {
        var response = await http.DeleteAsync($"api/issues/{issueId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<IssueSummary>> GetSimilarIssuesAsync(int issueId)
    {
        return await http.GetFromJsonAsync<List<IssueSummary>>($"api/issues/{issueId}/similar") ?? [];
    }

    public Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint) =>
        throw new NotSupportedException("Not available on client.");

    public Task RecordPageViewAsync(Guid userId, int projectId, PageType pageType) => Task.CompletedTask;

    public async Task<int> GetUnseenIssueCountAsync(int projectId, Guid userId)
    {
        try { return await http.GetFromJsonAsync<int>($"api/projects/{projectId}/unseen-issues?userId={userId}"); }
        catch { return 0; }
    }

    public async Task<List<int>> GetHistogramAsync(int issueId, int days = 14)
    {
        return await http.GetFromJsonAsync<List<int>>($"api/issues/{issueId}/histogram?days={days}") ?? [];
    }

    public async Task<Dictionary<int, List<int>>> GetBulkHistogramsAsync(List<int> issueIds, int days = 14)
    {
        var response = await http.PostAsJsonAsync($"api/issues/histogram/bulk?days={days}", issueIds);
        return await response.Content.ReadFromJsonAsync<Dictionary<int, List<int>>>() ?? [];
    }
}
