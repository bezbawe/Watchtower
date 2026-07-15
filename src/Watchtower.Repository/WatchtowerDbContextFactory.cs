using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Watchtower.Repository;

// Используется только инструментами EF Core (migrations/database update) во время разработки.
// Строку подключения берём из переменной окружения WATCHTOWER_DB, иначе — локальная dev-строка
// (совпадает с docker-compose). Это не боевые креды.
public class WatchtowerDbContextFactory : IDesignTimeDbContextFactory<WatchtowerDbContext>
{
    public WatchtowerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WATCHTOWER_DB")
            ?? "Host=localhost;Port=5432;Database=watchtower;Username=watchtower;Password=watchtower";

        var options = new DbContextOptionsBuilder<WatchtowerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WatchtowerDbContext(options);
    }
}
