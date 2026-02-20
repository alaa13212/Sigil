using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Cache;

internal class AppConfigCache(ICacheManager cacheManager) : IAppConfigCache
{
    // Wrapper so we can distinguish "not in cache" from "cached null value"
    private record CacheEntry(string? Value);

    private string Category => this.Category();

    public bool TryGet(string key, out string? value)
    {
        if (cacheManager.TryGet<CacheEntry>(Category, key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Set(string key, string? value) =>
        cacheManager.Set(Category, key, new CacheEntry(value));

    public void Invalidate(string key) =>
        cacheManager.Invalidate<IAppConfigCache>(key);
}
