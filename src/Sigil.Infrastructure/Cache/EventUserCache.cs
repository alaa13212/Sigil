using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal class EventUserCache(ICacheManager cacheManager) : IEventUserCache
{
    private string Category => this.Category();

    public bool TryGet(string identifier, out EventUser? user) =>
        cacheManager.TryGet(Category, identifier, out user);

    public void Set(EventUser user) =>
        cacheManager.Set(Category, user.UniqueIdentifier, user);
}
