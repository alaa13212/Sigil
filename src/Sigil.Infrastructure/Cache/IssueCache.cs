using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal class IssueCache(ICacheManager cacheManager) : IIssueCache
{
    private string Category => this.Category();

    public bool TryGet(int projectId, string fingerprint, out Issue? issue) =>
        cacheManager.TryGet(Category, $"{projectId}:{fingerprint}", out issue);

    public void Set(Issue issue) =>
        cacheManager.Set(Category, $"{issue.ProjectId}:{issue.Fingerprint}", issue);

    public void InvalidateAll() =>
        cacheManager.Invalidate<IIssueCache>();
}
