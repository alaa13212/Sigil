using System.Net.Http.Json;
using Sigil.Application.Interfaces;

namespace Sigil.Server.Client.Services;

public class ApiProjectConfigEditorService(HttpClient http) : IProjectConfigEditorService
{
    public async Task<Dictionary<string, string?>> GetAllAsync(int projectId)
    {
        return await http.GetFromJsonAsync<Dictionary<string, string?>>($"api/projects/{projectId}/config") ?? [];
    }

    public async Task SetAsync(int projectId, string key, string? value)
    {
        var response = await http.PutAsJsonAsync(
            $"api/projects/{projectId}/config/{Uri.EscapeDataString(key)}",
            new { Value = value });
        response.EnsureSuccessStatusCode();
    }
}
