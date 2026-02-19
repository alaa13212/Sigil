using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.infrastructure.Persistence;

internal class EfDatabaseMigrator(SigilDbContext dbContext) : IDatabaseMigrator
{
    public async Task MigrateAsync()
    {
        await dbContext.Database.MigrateAsync();
    }

    public async Task<DbConnectionStatus> CheckConnectionAsync()
    {
        if (await dbContext.Database.CanConnectAsync())
            return DbConnectionStatus.Connected;

        // Database didn't connect — check if the server itself is reachable
        // by connecting to the default "postgres" database
        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
            return DbConnectionStatus.ConnectionFailed;

        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };

        try
        {
            await using var conn = new NpgsqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            return DbConnectionStatus.DatabaseNotFound;
        }
        catch
        {
            return DbConnectionStatus.ConnectionFailed;
        }
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
