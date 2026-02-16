using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class ProjectCache(ICacheManager cacheManager, IProjectService projectService) : IProjectCache
{
    private string Category => this.Category();

    public async Task<Project?> GetProjectById(int id)
    {
        return await cacheManager.GetOrAddNullable(Category, id.ToString(), idString => projectService.GetProjectByIdAsync(Convert.ToInt32(idString)));
    }

}