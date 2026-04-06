using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;

namespace Sigil.Server.Client.Services;

public class ApiStackTraceFilterService(HttpClient http) : IStackTraceFilterService
{
    public async Task<List<StackTraceFilterResponse>> GetFiltersAsync(int projectId)
        => await http.GetFromJsonAsync<List<StackTraceFilterResponse>>($"api/projects/{projectId}/stack-trace-filters") ?? [];

    public async Task<StackTraceFilterResponse> CreateFilterAsync(int projectId, CreateStackTraceFilterRequest request)
    {
        var resp = await http.PostAsJsonAsync($"api/projects/{projectId}/stack-trace-filters", request);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StackTraceFilterResponse>())!;
    }

    public async Task<StackTraceFilterResponse?> UpdateFilterAsync(int projectId, int filterId, UpdateStackTraceFilterRequest request)
    {
        var resp = await http.PutAsJsonAsync($"api/projects/{projectId}/stack-trace-filters/{filterId}", request);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<StackTraceFilterResponse>();
    }

    public async Task<bool> DeleteFilterAsync(int projectId, int filterId)
    {
        var resp = await http.DeleteAsync($"api/projects/{projectId}/stack-trace-filters/{filterId}");
        return resp.IsSuccessStatusCode;
    }

}
