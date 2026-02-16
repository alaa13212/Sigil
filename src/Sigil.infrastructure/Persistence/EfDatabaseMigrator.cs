using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;

namespace Sigil.infrastructure.Persistence;

internal class EfDatabaseMigrator(SigilDbContext dbContext) : IDatabaseMigrator
{
    public async Task MigrateAsync()
    {
        await dbContext.Database.MigrateAsync();
    }
}