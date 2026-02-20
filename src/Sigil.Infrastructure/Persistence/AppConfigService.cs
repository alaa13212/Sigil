using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class AppConfigService(SigilDbContext dbContext, IAppConfigCache cache) : IAppConfigService
{
    public async Task<string?> GetAsync(string key)
    {
        if (cache.TryGet(key, out string? cached))
            return cached;

        var config = await dbContext.AppConfigs.FindAsync(key);
        var value = config?.Value;
        cache.Set(key, value);
        return value;
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
        cache.Invalidate(key);
    }

    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        return await dbContext.AppConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);
    }
}
