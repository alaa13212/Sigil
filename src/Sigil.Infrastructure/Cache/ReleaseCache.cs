using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class ReleaseCache(ICacheManager cacheManager) : IReleaseCache
{
    private string Category => this.Category();

    public bool TryGet(int projectId, string rawName, out Release? release) =>
        cacheManager.TryGet(Category, $"{projectId}:{rawName}", out release);

    public void Set(int projectId, Release release) =>
        cacheManager.Set(Category, $"{projectId}:{release.RawName}", release);
}