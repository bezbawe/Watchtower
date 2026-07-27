using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Watchtower.Entities.Enums;
using Watchtower.Ingestion;
using Watchtower.Ingestion.Buffering;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Normalization;
using Watchtower.LogGenerator;
using Watchtower.Repository;

namespace Watchtower.LogGenerator.Tests;

// Проверка критерия Фазы 3: запуск генератора наполняет БД потоком событий, и в потоке
// присутствуют заданные аномалии. Гоняем реальный конвейер приёма (как в Фазе 2) на
// Testcontainers PostgreSQL — без живого HTTP-хоста (чтобы не плодить orphan-процессы).
public class GeneratorPipelineTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task GeneratedStream_ReachesDatabase_WithAnomaliesPresent()
    {
        var options = new GeneratorOptions
        {
            Seed = 7,
            PeriodHours = 48,
            NormalEventsPerHour = 1,
            BruteForce = new BruteForceScenario { Enabled = true, Incidents = 2, Attempts = 10, WithinMinutes = 3 },
            OffHours = new OffHoursScenario { Enabled = true, Incidents = 3 },
            GeoAnomaly = new GeoAnomalyScenario { Enabled = true, Incidents = 2 },
        };

        var generated = SyntheticLogStream.Generate(options, DateTimeOffset.UtcNow);

        // Проходим ровно через провод: SyntheticEvent -> JSON -> LogEventDto (как принимает API).
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dtos = JsonSerializer.Deserialize<List<LogEventDto>>(JsonSerializer.Serialize(generated, web), web)!;
        Assert.Equal(generated.Count, dtos.Count);

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWatchtowerDbContext(_container.GetConnectionString());
        services.AddWatchtowerRepositories();
        services.AddWatchtowerIngestion(configuration);

        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
            await db.Database.MigrateAsync();
        }

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var hosted in hostedServices)
            await hosted.StartAsync(CancellationToken.None);

        var queue = provider.GetRequiredService<IEventIngestQueue>();
        var normalizer = provider.GetRequiredService<ILogEventNormalizer>();
        foreach (var dto in dtos)
            await queue.EnqueueAsync(normalizer.Normalize(dto));

        var count = await WaitForCountAsync(provider, generated.Count, TimeSpan.FromSeconds(30));

        foreach (var hosted in hostedServices)
            await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(generated.Count, count);

        using var readScope = provider.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
        var stored = await readDb.LogEvents.AsNoTracking().ToListAsync();

        // Brute-force: с одного IP >= порога неудачных логинов.
        var bruteSpike = stored
            .Where(e => e.EventType == EventType.LoginFailed && Scenario(e) == "brute_force")
            .GroupBy(e => e.SourceIp)
            .Any(g => g.Count() >= options.BruteForce.Attempts);
        Assert.True(bruteSpike, "expected a brute-force spike from a single IP in the database");

        // Off-hours: привилегированная активность помечена в потоке и доехала до БД.
        Assert.Contains(stored, e => Scenario(e) == "off_hours");

        // Geo-аномалия: один пользователь с двух разных IP.
        var impossibleTravel = stored
            .Where(e => Scenario(e) == "geo_anomaly")
            .GroupBy(e => e.Actor)
            .Any(g => g.Select(e => e.SourceIp).Distinct().Count() >= 2);
        Assert.True(impossibleTravel, "expected impossible-travel logins in the database");

        // Гео-обогащение проставлено для публичного IP атакующего (stub: 77.* -> RU).
        Assert.Contains(stored, e => e.SourceIp is not null && e.SourceIp.StartsWith("77.") && e.GeoCountry == "RU");
    }

    private static string? Scenario(Entities.Events.LogEvent e) =>
        e.Fields.TryGetValue("scenario", out var s) ? s : null;

    private static async Task<int> WaitForCountAsync(IServiceProvider provider, int expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
            var count = await db.LogEvents.CountAsync();
            if (count >= expected)
                return count;
            await Task.Delay(200);
        }

        using var finalScope = provider.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
        return await finalDb.LogEvents.CountAsync();
    }
}
