using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectConfigEditorService(SigilDbContext dbContext, IProjectConfigService projectConfigService) : IProjectConfigEditorService
{
    public async Task<Dictionary<string, string?>> GetAllAsync(int projectId)
    {
        return await dbContext.ProjectConfigs
            .Where(c => c.ProjectId == projectId)
            .ToDictionaryAsync(c => c.Key, c => c.Value);
    }

    public async Task SetAsync(int projectId, string key, string? value)
    {
        var existing = await dbContext.ProjectConfigs
            .AsTracking()
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Key == key);

        if (existing is not null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            dbContext.ProjectConfigs.Add(new ProjectConfig
            {
                ProjectId = projectId,
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        await projectConfigService.LoadAsync(projectId);
    }
}
