using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class ReleaseCache(ICacheManager cacheManager, IReleaseService releaseService) : IReleaseCache
{
    private string Category => this.Category();

    public async Task<List<Release>> BulkGetOrCreateReleaseAsync(int projectId, IEnumerable<string> rawValues)
    {
        List<Release> releases = [];
        List<string> newReleases = [];
        
        foreach (string rawValue in rawValues)
        {
            if (cacheManager.TryGet(Category, $"{projectId}:{rawValue}", out Release? release))
            {
                releases.Add(release);
            }
            else
            {
                newReleases.Add(rawValue);
            }
        }

        if(newReleases.Count > 0)
        {
            List<Release> createdReleases = await releaseService.BulkGetOrCreateReleasesAsync(projectId, newReleases);
            releases.AddRange(createdReleases);
            
            foreach (Release release in createdReleases) 
                cacheManager.Set(Category, $"{projectId}:{release.RawName}", release);
        }


        
        return releases;
    }
}