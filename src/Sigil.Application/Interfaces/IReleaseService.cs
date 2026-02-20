using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IReleaseService
{
    Task<List<Release>> BulkGetOrCreateReleasesAsync(int projectId, List<ParsedEvent> parsedEvents);
}