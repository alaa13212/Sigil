using System.Net.Http.Json;
using System.Text.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;

namespace Sigil.Server.Client.Services;

public class ApiProjectCorsService(HttpClient http) : IProjectCorsService
{
    public async Task<IReadOnlyList<string>> GetOriginsAsync(int projectId)
    {
        var response = await http.GetFromJsonAsync<ProjectCorsOriginsResponse>(
            $"api/projects/{projectId}/cors-origins");
        return response?.Origins ?? [];
    }

    public async Task<IReadOnlyList<string>> SetOriginsAsync(int projectId, SetCorsOriginsRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/projects/{projectId}/cors-origins", request);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadErrorAsync(response));

        var result = await response.Content.ReadFromJsonAsync<ProjectCorsOriginsResponse>();
        return result?.Origins ?? [];
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        string fallback = $"Request failed ({(int)response.StatusCode}).";
        string body = await response.Content.ReadAsStringAsync();

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("error", out JsonElement error) && error.GetString() is string message
                ? message
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
