using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;
using Sigil.infrastructure.Persistence;

namespace Sigil.infrastructure.Cache;

internal class IssueCache(ICacheManager cacheManager, IIssueService issueService, SigilDbContext dbContext) : IIssueCache
{
    private string Category => this.Category();

    public async Task<List<Issue>> BulkGetOrCreateIssues(Project project, ILookup<string, ParsedEvent> eventsByFingerprint)
    {
        List<Issue> results = [];
        List<IGrouping<string, ParsedEvent>> newIssues = [];
        
        foreach (IGrouping<string, ParsedEvent> eventGroup in eventsByFingerprint)
        {
            if (cacheManager.TryGet(Category, $"{project.Id}:{eventGroup.Key}", out Issue? issue))
            {
                results.Add(issue);
                dbContext.Attach(issue);
            }
            else
            {
                newIssues.Add(eventGroup);
            }
        }
        
        if(newIssues.Count > 0)
        {
            List<Issue> createdIssues = await issueService.BulkGetOrCreateIssuesAsync(project, newIssues);
            results.AddRange(createdIssues);
            
            foreach (Issue issue in createdIssues) 
                cacheManager.Set(Category, $"{project.Id}:{issue.Fingerprint}", issue);
        }
        
        return results;
    }
}