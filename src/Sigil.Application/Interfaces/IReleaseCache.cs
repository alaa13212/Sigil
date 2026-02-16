using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IReleaseCache : ICacheService
{
    static string ICacheService.CategoryName => "releases";
    
    Task<List<Release>> BulkGetOrCreateReleaseAsync(int projectId, IEnumerable<string> rawValues);
}