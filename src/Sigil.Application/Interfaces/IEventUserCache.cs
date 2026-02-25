using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IEventUserCache : ICacheService
{
    static string ICacheService.CategoryName => "event_users";

    bool TryGet(string identifier, out EventUser? user);
    void Set(EventUser user);
}
