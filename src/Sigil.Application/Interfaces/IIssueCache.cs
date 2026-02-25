using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IIssueCache : ICacheService
{
    static string ICacheService.CategoryName => "issues";

    bool TryGet(int projectId, string fingerprint, out Issue? issue);
    void Set(Issue issue);
    void InvalidateAll();
}
