using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IEventUserCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "event_users";

    bool TryGet(string identifier, out EventUser? user);
    void Set(EventUser user);
}
