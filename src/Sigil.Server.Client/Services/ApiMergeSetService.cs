using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.MergeSets;

namespace Sigil.Server.Client.Services;

public class ApiMergeSetService(HttpClient http) : IMergeSetService
{
    public async Task<MergeSetResponse> CreateAsync(int projectId, List<int> issueIds, Guid userId)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/merge-sets", new CreateMergeSetRequest(issueIds));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MergeSetResponse>())!;
    }

    public async Task<MergeSetResponse> AddIssueAsync(int mergeSetId, int issueId, Guid userId)
    {
        var response = await http.PostAsJsonAsync($"api/merge-sets/{mergeSetId}/issues", new AddIssueToMergeSetRequest(issueId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MergeSetResponse>())!;
    }

    public async Task RemoveIssueAsync(int mergeSetId, int issueId, Guid userId)
    {
        var response = await http.DeleteAsync($"api/merge-sets/{mergeSetId}/issues/{issueId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPrimaryAsync(int mergeSetId, int issueId)
    {
        var response = await http.PutAsJsonAsync($"api/merge-sets/{mergeSetId}/primary", new SetPrimaryRequest(issueId));
        response.EnsureSuccessStatusCode();
    }

    public async Task<MergeSetResponse?> GetByIdAsync(int mergeSetId)
    {
        try
        {
            return await http.GetFromJsonAsync<MergeSetResponse>($"api/merge-sets/{mergeSetId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task RefreshAggregatesAsync(IEnumerable<int> mergeSetIds) =>
        throw new NotSupportedException("Not available on client.");
}
