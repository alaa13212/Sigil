using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Reingestion;

namespace Sigil.Server.Client.Services;

public class ApiReingestionService(HttpClient http) : IReingestionService
{
    public async Task<ReingestionJobResponse> StartProjectReingestionAsync(int projectId, Guid? userId = null)
    {
        var response = await http.PostAsync($"api/projects/{projectId}/reingest", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReingestionJobResponse>())!;
    }

    public async Task<ReingestionJobResponse> StartIssueReingestionAsync(int issueId, Guid? userId = null)
    {
        var response = await http.PostAsync($"api/issues/{issueId}/reingest", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReingestionJobResponse>())!;
    }

    public async Task<ReingestionJobResponse?> GetJobStatusAsync(int jobId) =>
        await http.GetFromJsonAsync<ReingestionJobResponse>($"api/reingestion/{jobId}");

    public async Task<List<ReingestionJobResponse>> GetJobsForProjectAsync(int projectId) =>
        await http.GetFromJsonAsync<List<ReingestionJobResponse>>($"api/projects/{projectId}/reingestion") ?? [];

    public async Task<bool> CancelJobAsync(int jobId)
    {
        var response = await http.PostAsync($"api/reingestion/{jobId}/cancel", null);
        return response.IsSuccessStatusCode;
    }
}
