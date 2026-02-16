using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IIssueCache : ICacheService
{
    static string ICacheService.CategoryName => "issues";
    
    Task<List<Issue>> BulkGetOrCreateIssues(Project project, ILookup<string, ParsedEvent> eventsByFingerprint);
}