using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IReleaseCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "releases";

    bool TryGet(int projectId, string rawName, out Release? release);
    void Set(int projectId, Release release);
}
