using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventUserCache : ICacheService
{
    static string ICacheService.CategoryName => "event_users";
    
    Task<IReadOnlyCollection<EventUser>> BulkGetEventUsers(IEnumerable<ParsedEventUser> parsedEventUsers);
}