using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventParsingContextBuilder
{
    Task<EventParsingContext> BuildAsync(int projectId);
}
