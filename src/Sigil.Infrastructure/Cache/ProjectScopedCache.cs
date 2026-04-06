using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Cache;

internal class ProjectScopedCache<TInterface, T>(ICacheManager cacheManager)
    where TInterface : ICacheService
{
    private static string Category => TInterface.CategoryName;

    public bool TryGet(int projectId, out List<T>? items) =>
        cacheManager.TryGet(Category, projectId.ToString(), out items);

    public void Set(int projectId, List<T> items) =>
        cacheManager.Set(Category, projectId.ToString(), items);

    public void Invalidate(int projectId) =>
        cacheManager.Invalidate<TInterface>(projectId.ToString());
}
