using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Server.Client.Auth;

namespace Sigil.Server.Client.Services;

public class ApiAuthService(HttpClient http, AuthenticationStateProvider authStateProvider) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/account/login", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            return AuthResult.Failure(body?.Errors ?? ["Invalid email or password."]);
        }

        var user = await response.Content.ReadFromJsonAsync<UserInfo>();
        if (authStateProvider is CookieAuthenticationStateProvider cookie)
            cookie.NotifyAuthStateChanged();

        return AuthResult.Success(user!);
    }

    public async Task LogoutAsync()
    {
        await http.PostAsync("api/account/logout", null);
        if (authStateProvider is CookieAuthenticationStateProvider cookie)
            cookie.NotifyAuthStateChanged();
    }

    public async Task<List<UserInfo>> GetAllUsersAsync()
    {
        return await http.GetFromJsonAsync<List<UserInfo>>("api/account/users") ?? [];
    }

    public async Task<InviteResult> InviteUserAsync(InviteRequest request)
    {
        var response = await http.PostAsJsonAsync("api/account/invite", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            return InviteResult.Failure(body?.Errors ?? ["Failed to invite user."]);
        }

        var result = await response.Content.ReadFromJsonAsync<InviteBody>();
        return InviteResult.Success(result!.Email, result.ActivationToken);
    }

    public async Task<AuthResult> ActivateAccountAsync(ActivateRequest request)
    {
        var response = await http.PostAsJsonAsync("api/account/activate", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            return AuthResult.Failure(body?.Errors ?? ["Activation failed."]);
        }

        return AuthResult.Success((await response.Content.ReadFromJsonAsync<UserInfo>())!);
    }

    private record ErrorBody(IReadOnlyList<string> Errors);
    private record InviteBody(string Email, string ActivationToken);
}
