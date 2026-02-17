using Sigil.Application.Models.Auth;

namespace Sigil.Application.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync();
    Task<DbConnectionStatus> CheckConnectionAsync();
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync();
    Task<IReadOnlyList<string>> GetAppliedMigrationsAsync();
}
