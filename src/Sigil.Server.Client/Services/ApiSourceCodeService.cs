using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;

namespace Sigil.Server.Client.Services;

public class ApiSourceCodeService(HttpClient http) : ISourceCodeService
{
    public async Task<List<SourceCodeProviderResponse>> GetProvidersAsync() =>
        await http.GetFromJsonAsync<List<SourceCodeProviderResponse>>("api/source-code-providers") ?? [];

    public async Task<SourceCodeProviderResponse> AddProviderAsync(CreateProviderRequest request, Guid createdByUserId)
    {
        var response = await http.PostAsJsonAsync("api/source-code-providers", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SourceCodeProviderResponse>())!;
    }

    public async Task<bool> DeleteProviderAsync(int id)
    {
        var response = await http.DeleteAsync($"api/source-code-providers/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TestConnectionAsync(int providerId, string testOwner, string testRepo)
    {
        var response = await http.PostAsJsonAsync($"api/source-code-providers/{providerId}/test",
            new { owner = testOwner, repo = testRepo });
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProjectRepositoryResponse>> GetRepositoriesAsync(int projectId) =>
        await http.GetFromJsonAsync<List<ProjectRepositoryResponse>>(
            $"api/projects/{projectId}/repositories") ?? [];

    public async Task<ProjectRepositoryResponse> LinkRepositoryAsync(int projectId, LinkRepositoryRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/repositories", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectRepositoryResponse>())!;
    }

    public async Task<bool> UnlinkRepositoryAsync(int projectId, int repositoryId)
    {
        var response = await http.DeleteAsync($"api/projects/{projectId}/repositories/{repositoryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<SourceContextResponse?> GetSourceContextForEventAsync(long eventId, string filename, int line)
    {
        var url = $"api/events/{eventId}/source-context?filename={Uri.EscapeDataString(filename)}&line={line}";
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SourceContextResponse>();
    }

    public async Task<CommitInfo?> GetCommitInfoAsync(int projectId, string commitSha)
    {
        var response = await http.GetAsync($"api/projects/{projectId}/commits/{commitSha}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CommitInfo>();
    }
}
