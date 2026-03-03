using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Used by the digestion pipeline to bulk-create issues.</summary>
public interface IIssueIngestionService
{
    Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint);
}
