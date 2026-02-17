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
            return AuthResult.Failure("Invalid email or password.");

        var user = await response.Content.ReadFromJsonAsync<UserInfo>();
        if (authStateProvider is CookieAuthenticationStateProvider cookie)
            cookie.NotifyAuthStateChanged();

        return AuthResult.Success(user!);
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var response = await http.PostAsJsonAsync("api/account/register", request);
        if (!response.IsSuccessStatusCode)
            return AuthResult.Failure("Registration failed.");

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
}
