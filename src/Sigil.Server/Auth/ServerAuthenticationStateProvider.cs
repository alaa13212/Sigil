using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Auth;

/// <summary>
/// Reads auth state from HttpContext and persists it via PersistentComponentState
/// so the WASM client can pick it up without an extra /api/account/me round-trip.
/// </summary>
public sealed class PersistingServerAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
{
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private Task<AuthenticationState>? _authStateTask;

    public PersistingServerAuthenticationStateProvider(
        PersistentComponentState state)
    {
        _state = state;

        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _authStateTask = task;
    }

    private async Task OnPersistingAsync()
    {
        if (_authStateTask is null)
            throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");

        var authState = await _authStateTask;
        var principal = authState.User;

        if (principal.Identity?.IsAuthenticated != true)
            return;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.FindFirst(ClaimTypes.Name)?.Value;
        var displayName = principal.FindFirst("DisplayName")?.Value;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (userId is not null && email is not null)
        {
            _state.PersistAsJson(nameof(UserInfo), new UserInfo(
                Guid.Parse(userId),
                email,
                displayName,
                DateTime.UtcNow,
                null,
                roles));
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
