using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;

namespace Sigil.Server.Client.Services;

public class ApiEventFilterService(HttpClient http) : IEventFilterService
{
    public async Task<List<EventFilterResponse>> GetFiltersAsync(int projectId)
    {
        return await http.GetFromJsonAsync<List<EventFilterResponse>>($"api/projects/{projectId}/filters") ?? [];
    }

    public async Task<EventFilterResponse> CreateFilterAsync(int projectId, CreateFilterRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/filters", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventFilterResponse>())!;
    }

    public async Task<EventFilterResponse?> UpdateFilterAsync(int projectId, int filterId, UpdateFilterRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/projects/{projectId}/filters/{filterId}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<EventFilterResponse>();
    }

    public async Task<bool> DeleteFilterAsync(int projectId, int filterId)
    {
        var response = await http.DeleteAsync($"api/projects/{projectId}/filters/{filterId}");
        return response.IsSuccessStatusCode;
    }

}
