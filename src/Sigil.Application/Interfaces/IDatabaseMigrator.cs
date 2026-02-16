namespace Sigil.Application.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync();
}