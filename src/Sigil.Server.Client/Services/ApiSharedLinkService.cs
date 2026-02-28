using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Shared;

namespace Sigil.Server.Client.Services;

public class ApiSharedLinkService(HttpClient http) : ISharedLinkService
{
    public async Task<SharedIssueLinkResponse> CreateLinkAsync(int issueId, Guid userId, string hostUrl, TimeSpan? duration = null)
    {
        var response = await http.PostAsJsonAsync($"api/issues/{issueId}/share", new CreateSharedLinkRequest(duration));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SharedIssueLinkResponse>())!;
    }

    public async Task<SharedIssueViewResponse?> ValidateLinkAsync(Guid token)
    {
        try
        {
            return await http.GetFromJsonAsync<SharedIssueViewResponse>($"api/shared/{token}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<PagedResponse<EventSummary>?> GetSharedEventsAsync(Guid token, int page, int pageSize)
    {
        try
        {
            return await http.GetFromJsonAsync<PagedResponse<EventSummary>>(
                $"api/shared/{token}/events?page={page}&pageSize={pageSize}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<IssueEventDetailResponse?> GetSharedEventDetailAsync(Guid token, long eventId)
    {
        try
        {
            return await http.GetFromJsonAsync<IssueEventDetailResponse>($"api/shared/{token}/events/{eventId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> RevokeLinkAsync(Guid token)
    {
        var response = await http.DeleteAsync($"api/shared/{token}");
        return response.IsSuccessStatusCode;
    }
}
