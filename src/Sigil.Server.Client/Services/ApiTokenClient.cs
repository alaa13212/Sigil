using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.ApiTokens;

namespace Sigil.Server.Client.Services;

public class ApiTokenClient(HttpClient http) : IApiTokenService
{
    public async Task<ApiTokenCreatedResponse> CreateAsync(Guid userId, string name, DateTime? expiresAt = null)
    {
        var response = await http.PostAsJsonAsync("api/api-tokens", new CreateApiTokenRequest(name, expiresAt));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ApiTokenCreatedResponse>()
            ?? throw new InvalidOperationException("The API token could not be created.");
    }

    public async Task<List<ApiTokenSummary>> ListAsync(Guid userId)
    {
        return await http.GetFromJsonAsync<List<ApiTokenSummary>>("api/api-tokens") ?? [];
    }

    public async Task<bool> RevokeAsync(Guid userId, int tokenId)
    {
        var response = await http.DeleteAsync($"api/api-tokens/{tokenId}");
        return response.IsSuccessStatusCode;
    }

    public Task<ApiTokenIdentity?> AuthenticateAsync(string presentedToken)
        => throw new NotSupportedException("API tokens are authenticated on the server.");
}
