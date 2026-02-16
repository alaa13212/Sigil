using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IIssueService
{
    Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint);
}
