using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class ProjectCache(ICacheManager cacheManager) : IProjectCache
{
    private const string ListKey = "__list__";
    private string Category => this.Category();

    public bool TryGet(int id, out Project? project) =>
        cacheManager.TryGet(Category, id.ToString(), out project);

    public void Set(Project project) =>
        cacheManager.Set(Category, project.Id.ToString(), project);

    public void Invalidate(int id) =>
        cacheManager.Invalidate<IProjectCache>(id.ToString());

    public bool TryGetList(out List<Project>? projects) =>
        cacheManager.TryGet(Category, ListKey, out projects);

    public void SetList(List<Project> projects) =>
        cacheManager.Set(Category, ListKey, projects);

    public void InvalidateList() =>
        cacheManager.Invalidate<IProjectCache>(ListKey);
}
