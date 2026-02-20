using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IReleaseService
{
    Task<Release> CreateReleaseAsync(int projectId, string rawValue);
    Task<List<Release>> BulkGetOrCreateReleasesAsync(int projectId, List<ParsedEvent> parsedEvents);
}