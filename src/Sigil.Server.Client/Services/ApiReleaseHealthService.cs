using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Releases;

namespace Sigil.Server.Client.Services;

public class ApiReleaseHealthService(HttpClient http) : IReleaseHealthService
{
    public async Task<PagedResponse<ReleaseHealthSummary>> GetReleaseHealthAsync(int projectId, int page = 1, int pageSize = 20)
    {
        return await http.GetFromJsonAsync<PagedResponse<ReleaseHealthSummary>>(
            $"api/projects/{projectId}/releases?page={page}&pageSize={pageSize}")
            ?? new PagedResponse<ReleaseHealthSummary>([], 0, page, pageSize);
    }

    public async Task<ReleaseDetailResponse?> GetReleaseDetailAsync(int releaseId)
    {
        try { return await http.GetFromJsonAsync<ReleaseDetailResponse>($"api/releases/{releaseId}"); }
        catch (HttpRequestException) { return null; }
    }

    public async Task<int> GetUnseenReleaseCountAsync(int projectId, Guid userId)
    {
        try { return await http.GetFromJsonAsync<int>($"api/projects/{projectId}/unseen-releases?userId={userId}"); }
        catch { return 0; }
    }
}
