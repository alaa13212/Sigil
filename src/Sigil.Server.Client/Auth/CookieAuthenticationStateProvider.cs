using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Auth;

public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly HttpClient _http;
    private readonly Task<AuthenticationState> _initialState;

    public CookieAuthenticationStateProvider(HttpClient http, PersistentComponentState state)
    {
        _http = http;

        // Try to read persisted auth state from server prerender
        if (state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) && userInfo is not null)
        {
            _initialState = Task.FromResult(BuildAuthState(userInfo));
        }
        else
        {
            _initialState = Task.FromResult(Anonymous);
        }
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // On first call, return persisted state (avoids /api round-trip)
        var initial = await _initialState;
        if (initial.User.Identity?.IsAuthenticated == true)
            return initial;

        return Anonymous;
    }

    public void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(FetchAuthStateAsync());
    }

    private async Task<AuthenticationState> FetchAuthStateAsync()
    {
        try
        {
            var user = await _http.GetFromJsonAsync<UserInfo>("api/account/me");
            return user is null ? Anonymous : BuildAuthState(user);
        }
        catch
        {
            return Anonymous;
        }
    }

    private static AuthenticationState BuildAuthState(UserInfo user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email),
        };

        // Add all role claims
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
