using Microsoft.JSInterop;
using Sigil.Application.Interfaces;

namespace Sigil.Server.Client.Services;

/// <summary>
/// Persists user preferences in browser localStorage via JS interop.
/// Gracefully no-ops during SSR pre-rendering when JS is unavailable.
/// </summary>
public class LocalStoragePreferenceService(IJSRuntime js) : IUserPreferenceService
{
    public async Task<string?> GetAsync(string key)
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", key); }
        catch { return null; }
    }

    public async Task SetAsync(string key, string value)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", key, value); }
        catch { /* ignore during prerendering */ }
    }
}
