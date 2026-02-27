using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Recommendations;
using Sigil.Domain.Entities;

namespace Sigil.Server.Client.Services;

public class ApiRecommendationService(HttpClient http) : IRecommendationService
{
    public async Task<int> GetRecommendationCountAsync(int projectId)
    {
        return await http.GetFromJsonAsync<int>($"api/projects/{projectId}/recommendations/count");
    }

    public async Task<List<ProjectRecommendationResponse>> GetRecommendationsAsync(int projectId)
    {
        return await http.GetFromJsonAsync<List<ProjectRecommendationResponse>>($"api/projects/{projectId}/recommendations") ?? [];
    }

    public async Task RunAnalyzersAsync(Project project)
    {
        HttpResponseMessage response = await http.PostAsync($"api/projects/{project.Id}/recommendations/refresh", new StringContent(""));
        response.EnsureSuccessStatusCode();
    }

    public async Task DismissAsync(int recommendationId)
    {
        HttpResponseMessage response = await http.PutAsJsonAsync($"api/recommendations/{recommendationId}/dismiss", new StringContent(""));
        response.EnsureSuccessStatusCode();
    }
}