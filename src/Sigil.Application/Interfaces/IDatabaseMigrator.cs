namespace Sigil.Application.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync();
    Task<bool> CanConnectAsync();
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync();
    Task<IReadOnlyList<string>> GetAppliedMigrationsAsync();
}
