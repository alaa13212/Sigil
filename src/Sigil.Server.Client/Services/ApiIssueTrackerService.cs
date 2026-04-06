using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;

namespace Sigil.Server.Client.Services;

public class ApiIssueTrackerService(HttpClient http) : IIssueTrackerService
{
    public async Task<IssueTrackerConfigResponse> AddConfigAsync(int projectId, CreateTrackerConfigRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/issue-tracker-configs", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssueTrackerConfigResponse>())!;
    }

    public async Task<List<IssueTrackerConfigResponse>> GetConfigsAsync(int projectId) =>
        await http.GetFromJsonAsync<List<IssueTrackerConfigResponse>>(
            $"api/projects/{projectId}/issue-tracker-configs") ?? [];

    public async Task<bool> UpdateConfigAsync(int projectId, int configId, UpdateTrackerConfigRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/projects/{projectId}/issue-tracker-configs/{configId}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteConfigAsync(int projectId, int configId)
    {
        var response = await http.DeleteAsync($"api/projects/{projectId}/issue-tracker-configs/{configId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TestConfigAsync(int projectId, int configId)
    {
        var response = await http.PostAsync($"api/projects/{projectId}/issue-tracker-configs/{configId}/test", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<ExternalIssueLinkResponse> CreateExternalIssueAsync(int issueId, int trackerConfigId)
    {
        var response = await http.PostAsJsonAsync($"api/issues/{issueId}/external-links",
            new { trackerConfigId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExternalIssueLinkResponse>())!;
    }

    public async Task<List<ExternalIssueLinkResponse>> GetLinksAsync(int issueId) =>
        await http.GetFromJsonAsync<List<ExternalIssueLinkResponse>>(
            $"api/issues/{issueId}/external-links") ?? [];

    public async Task<bool> UnlinkAsync(int projectId, int linkId)
    {
        var response = await http.DeleteAsync($"api/projects/{projectId}/external-links/{linkId}");
        return response.IsSuccessStatusCode;
    }
}
