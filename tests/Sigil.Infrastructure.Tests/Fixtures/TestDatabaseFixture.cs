using Microsoft.EntityFrameworkCore;
using Sigil.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Sigil.Infrastructure.Tests.Fixtures;

public class TestDatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:17")
            .Build();
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var context = new SigilDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
