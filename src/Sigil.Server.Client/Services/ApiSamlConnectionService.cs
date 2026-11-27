using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;

namespace Sigil.Server.Client.Services;

public class ApiSamlConnectionService(HttpClient http) : ISamlConnectionService
{
    public async Task<List<SamlConnectionResponse>> GetConnectionsAsync() =>
        await http.GetFromJsonAsync<List<SamlConnectionResponse>>("api/saml/connections") ?? [];

    public async Task<List<SamlLoginOptionResponse>> GetEnabledConnectionsAsync() =>
        await http.GetFromJsonAsync<List<SamlLoginOptionResponse>>("api/saml/connections/available") ?? [];

    public async Task<SamlEndpointsResponse> GetEndpointsAsync() =>
        await http.GetFromJsonAsync<SamlEndpointsResponse>("api/saml/connections/endpoints")
        ?? new SamlEndpointsResponse(SamlDefaults.DefaultEntityId, SamlDefaults.ModulePath, SamlDefaults.ModulePath + "/Acs");

    public async Task<SamlConnectionResponse?> GetConnectionAsync(Guid id)
    {
        var connections = await GetConnectionsAsync();
        return connections.Find(c => c.Id == id);
    }

    public async Task<SamlConnectionResponse> CreateConnectionAsync(SamlConnectionRequest request)
    {
        var response = await http.PostAsJsonAsync("api/saml/connections", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SamlConnectionResponse>())!;
    }

    public async Task<SamlConnectionResponse?> UpdateConnectionAsync(Guid id, SamlConnectionRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/saml/connections/{id}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SamlConnectionResponse>();
    }

    public async Task<bool> DeleteConnectionAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/saml/connections/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetMetadataAsync(Guid id, string? metadataXml, DateTime? fetchedAt, string? error) =>
        false;

    public async Task<bool> RefreshMetadataAsync(Guid id)
    {
        var response = await http.PostAsync($"api/saml/connections/{id}/refresh-metadata", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SamlPendingUserResponse>> GetPendingUsersAsync() =>
        await http.GetFromJsonAsync<List<SamlPendingUserResponse>>("api/saml/users/pending") ?? [];

    public async Task<bool> ApproveUserAsync(Guid userId)
    {
        var response = await http.PostAsync($"api/saml/users/{userId}/approve", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RejectUserAsync(Guid userId)
    {
        var response = await http.PostAsync($"api/saml/users/{userId}/reject", null);
        return response.IsSuccessStatusCode;
    }
}
