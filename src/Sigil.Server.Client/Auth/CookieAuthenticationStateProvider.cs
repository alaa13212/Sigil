using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Auth;

public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly PersistentComponentState _state;
    private readonly ICurrentUserService _currentUserService;
    private Task<AuthenticationState>? _authState;

    public CookieAuthenticationStateProvider(PersistentComponentState state, ICurrentUserService currentUserService)
    {
        _state = state;
        _currentUserService = currentUserService;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        _authState ??= ResolveAsync();

    private async Task<AuthenticationState> ResolveAsync()
    {
        try
        {
            if (_state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) && userInfo is not null)
                return BuildAuthState(userInfo);

            var currentUser = await _currentUserService.GetCurrentUserAsync();
            return currentUser is null ? Anonymous : BuildAuthState(currentUser);
        }
        catch
        {
            return Anonymous;
        }
    }

    public void NotifyAuthStateChanged(UserInfo? user = null)
    {
        _authState = Task.FromResult(user is not null ? BuildAuthState(user) : Anonymous);
        NotifyAuthenticationStateChanged(_authState);
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
