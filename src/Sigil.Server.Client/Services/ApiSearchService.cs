using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Search;

namespace Sigil.Server.Client.Services;

public class ApiSearchService(HttpClient http) : ISearchService
{
    public async Task<SearchResultsResponse> SearchAsync(string query, int? projectId)
    {
        var url = $"api/search?q={Uri.EscapeDataString(query)}{(projectId.HasValue ? $"&projectId={projectId}" : "")}";
        return await http.GetFromJsonAsync<SearchResultsResponse>(url)
               ?? new SearchResultsResponse([], [], []);
    }
}
