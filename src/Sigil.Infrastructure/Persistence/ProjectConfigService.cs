using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectConfigService(SigilDbContext dbContext, IProjectConfigCache cache) : IProjectConfigService
{
    public async Task<string?> GetAsync(int projectId, string key)
    {
        if (cache.TryGet(projectId, key, out string? cached))
            return cached;

        var config = await dbContext.ProjectConfigs
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Key == key);
        var value = config?.Value;
        cache.Set(projectId, key, value);
        return value;
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
        cache.Invalidate(projectId, key);
    }

    public async Task<Dictionary<string, string?>> GetAllAsync(int projectId)
    {
        return await dbContext.ProjectConfigs
            .Where(c => c.ProjectId == projectId)
            .ToDictionaryAsync(c => c.Key, c => c.Value);
    }
}
