using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Watchtower.Repository.Tests;

// Поднимает эфемерный PostgreSQL в контейнере на время тестов и накатывает миграции.
// Образ postgres:16 переиспользуется из docker-compose (без сетевого pull).
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public WatchtowerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WatchtowerDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new WatchtowerDbContext(options);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
