using System.Net.Http.Json;
using Sigil.Application.Interfaces;

namespace Sigil.Server.Client.Services;

public class ApiAppConfigEditorService(HttpClient http) : IAppConfigEditorService
{
    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        return await http.GetFromJsonAsync<Dictionary<string, string?>>("api/admin/app-config") ?? [];
    }

    public async Task SetAsync(string key, string? value)
    {
        var response = await http.PutAsJsonAsync($"api/admin/app-config/{Uri.EscapeDataString(key)}", new { Value = value });
        response.EnsureSuccessStatusCode();
    }
}
