using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;

namespace Sigil.infrastructure.Persistence;

internal class EfDatabaseMigrator(SigilDbContext dbContext) : IDatabaseMigrator
{
    public async Task MigrateAsync()
    {
        await dbContext.Database.MigrateAsync();
    }

    public async Task<bool> CanConnectAsync()
    {
        return await dbContext.Database.CanConnectAsync();
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync()
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        return pending.ToList();
    }

    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync()
    {
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        return applied.ToList();
    }
}
