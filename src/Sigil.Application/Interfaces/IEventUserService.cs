using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventUserService
{
    Task<List<EventUser>> BulkGetOrCreateEventUsersAsync(IReadOnlyCollection<ParsedEventUser> eventUsers);
}