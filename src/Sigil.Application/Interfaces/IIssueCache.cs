using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IIssueCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "issues";

    bool TryGet(int projectId, string fingerprint, out Issue? issue);
    void Set(Issue issue);
    void InvalidateAll();
}
