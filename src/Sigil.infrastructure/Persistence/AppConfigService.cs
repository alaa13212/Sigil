using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence;

internal class AppConfigService(SigilDbContext dbContext) : IAppConfigService
{
    public async Task<string?> GetAsync(string key)
    {
        var config = await dbContext.AppConfigs.FindAsync(key);
        return config?.Value;
    }

    public async Task SetAsync(string key, string? value)
    {
        var existing = await dbContext.AppConfigs.AsTracking().FirstOrDefaultAsync(c => c.Key == key);
        if (existing is not null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            dbContext.AppConfigs.Add(new AppConfig
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        return await dbContext.AppConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);
    }
}
