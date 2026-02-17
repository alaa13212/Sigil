using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Server.Client.Services;

public class ApiIssueActivityService(HttpClient http) : IIssueActivityService
{
    public async Task<PagedResponse<ActivityResponse>> GetActivitySummariesAsync(int issueId, int page = 1, int pageSize = 50)
    {
        return await http.GetFromJsonAsync<PagedResponse<ActivityResponse>>(
            $"api/issues/{issueId}/activity?page={page}&pageSize={pageSize}")
            ?? new PagedResponse<ActivityResponse>([], 0, page, pageSize);
    }

    public async Task<(List<IssueActivity> Items, int TotalCount)> GetActivitiesForIssueAsync(int issueId, int page = 1, int pageSize = 50)
    {
        var response = await GetActivitySummariesAsync(issueId, page, pageSize);
        var items = response.Items.Select(a => new IssueActivity
        {
            Id = a.Id,
            Action = a.Action,
            Message = a.Message,
            Timestamp = a.Timestamp,
            IssueId = issueId,
            UserId = a.UserId,
        }).ToList();

        return (items, response.TotalCount);
    }

    public Task<IssueActivity> LogActivityAsync(int issueId, Guid userId, IssueActivityAction action, string? message = null) =>
        throw new NotSupportedException("Not available on client.");
}
