using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Auth;

public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private Task<AuthenticationState> _initialState;

    public CookieAuthenticationStateProvider(PersistentComponentState state)
    {
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

    public void NotifyAuthStateChanged(UserInfo? user = null)
    {
        _initialState = Task.FromResult(user is not null ? BuildAuthState(user) : Anonymous);
        NotifyAuthenticationStateChanged(_initialState);
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
