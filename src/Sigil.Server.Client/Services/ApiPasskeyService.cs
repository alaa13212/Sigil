using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Services;

public class ApiPasskeyService(HttpClient http) : IPasskeyService
{
    public async Task<PasskeyRegistrationOptions> GetRegistrationOptionsAsync(Guid userId)
    {
        var response = await http.PostAsync("api/passkey/register/options", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PasskeyRegistrationOptions>())!;
    }

    public async Task<AuthResult> CompleteRegistrationAsync(Guid userId, PasskeyRegistrationResponse request)
    {
        var response = await http.PostAsJsonAsync("api/passkey/register/complete", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            return AuthResult.Failure(body?.Errors ?? ["Registration failed."]);
        }
        var user = await response.Content.ReadFromJsonAsync<UserInfo>();
        return AuthResult.Success(user!);
    }

    public async Task<PasskeyAssertionOptions> GetAssertionOptionsAsync()
    {
        var response = await http.PostAsync("api/passkey/login/options", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PasskeyAssertionOptions>())!;
    }

    public async Task<AuthResult> CompleteAssertionAsync(PasskeyAssertionResponse request)
    {
        var response = await http.PostAsJsonAsync("api/passkey/login/complete", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            return AuthResult.Failure(body?.Errors ?? ["Authentication failed."]);
        }
        var user = await response.Content.ReadFromJsonAsync<UserInfo>();
        return AuthResult.Success(user!);
    }

    public async Task<List<PasskeyInfo>> GetPasskeysAsync(Guid userId)
    {
        return await http.GetFromJsonAsync<List<PasskeyInfo>>("api/passkey/list") ?? [];
    }

    public async Task<bool> DeletePasskeyAsync(Guid userId, int passkeyId)
    {
        var response = await http.DeleteAsync($"api/passkey/{passkeyId}");
        return response.IsSuccessStatusCode;
    }

    private record ErrorBody(IReadOnlyList<string> Errors);
}
