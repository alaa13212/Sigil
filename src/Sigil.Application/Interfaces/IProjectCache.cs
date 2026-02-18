using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IProjectCache : ICacheService
{
    static string ICacheService.CategoryName => "projects";

    bool TryGet(int id, out Project? project);
    void Set(Project project);
    void Invalidate(int id);

    bool TryGetList(out List<Project>? projects);
    void SetList(List<Project> projects);
    void InvalidateList();
}
