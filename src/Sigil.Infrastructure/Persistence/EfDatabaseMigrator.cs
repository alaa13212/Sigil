using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.Infrastructure.Persistence;

internal class EfDatabaseMigrator(
    SigilDbContext dbContext,
    IServiceProvider serviceProvider
) : IDatabaseMigrator
{
    public async Task MigrateAsync()
    {
        await dbContext.Database.MigrateAsync();

        foreach (IAsyncStartupInitializer service in serviceProvider.GetServices<IAsyncStartupInitializer>()) 
            await service.InitializeAsync();
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
