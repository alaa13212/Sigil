using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class AppConfigEditorService(SigilDbContext dbContext, IAppConfigService appConfigService) : IAppConfigEditorService
{
    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        return await dbContext.AppConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);
    }

    public async Task SetAsync(string key, string? value)
    {
        AppConfig? existing = await dbContext.AppConfigs.AsTracking().FirstOrDefaultAsync(c => c.Key == key);
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
        await appConfigService.LoadAsync();
    }
}
