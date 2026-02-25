using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Cache;

internal class ProjectConfigCache(ICacheManager cacheManager) : IProjectConfigCache
{
    private record CacheEntry(string? Value);

    private string Category => this.Category();

    // Composite cache key: "projectId:configKey"
    private static string CacheKey(int projectId, string key) => $"{projectId}:{key}";

    public bool TryGet(int projectId, string key, out string? value)
    {
        if (cacheManager.TryGet<CacheEntry>(Category, CacheKey(projectId, key), out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Set(int projectId, string key, string? value) =>
        cacheManager.Set(Category, CacheKey(projectId, key), new CacheEntry(value));

    public void Invalidate(int projectId, string key) =>
        cacheManager.Invalidate<IProjectConfigCache>(CacheKey(projectId, key));

    public void InvalidateAll(int projectId) =>
        cacheManager.Invalidate<IProjectConfigCache>();
}
