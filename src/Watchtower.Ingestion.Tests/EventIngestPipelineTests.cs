using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Watchtower.Ingestion.Buffering;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Normalization;
using Watchtower.Repository;

namespace Watchtower.Ingestion.Tests;

// Интеграционный прогон всего конвейера приёма на реальном PostgreSQL, но без HTTP-слоя:
// реальные DI-регистрации (AddWatchtowerDbContext + AddWatchtowerRepositories +
// AddWatchtowerIngestion) → Channel-буфер → BackgroundService → батчевая запись в БД.
public class EventIngestPipelineTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Batch_EnqueuedEvents_ReachDatabase_ViaBackgroundService()
    {
        const int batchSize = 25;

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWatchtowerDbContext(_container.GetConnectionString());
        services.AddWatchtowerRepositories();
        services.AddWatchtowerIngestion(configuration);

        await using var provider = services.BuildServiceProvider();

        // Схема.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
            await db.Database.MigrateAsync();
        }

        // Запускаем consumer (BackgroundService).
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var hosted in hostedServices)
            await hosted.StartAsync(CancellationToken.None);

        // Producer: нормализуем и кладём батч в буфер.
        var queue = provider.GetRequiredService<IEventIngestQueue>();
        var normalizer = provider.GetRequiredService<ILogEventNormalizer>();
        for (var i = 0; i < batchSize; i++)
        {
            var dto = new LogEventDto
            {
                Source = "auth-service",
                Severity = i % 2 == 0 ? "info" : "warning",
                EventType = "login_failed",
                Message = $"event {i}",
                Actor = $"user{i % 5}",
                SourceIp = "203.0.113.7",
                Fields = new Dictionary<string, string> { ["attempt"] = i.ToString() },
            };
            await queue.EnqueueAsync(normalizer.Normalize(dto));
        }

        // Ждём, пока consumer сольёт буфер в БД.
        var count = await WaitForCountAsync(provider, batchSize, TimeSpan.FromSeconds(15));

        foreach (var hosted in hostedServices)
            await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(batchSize, count);

        // Гео-обогащение доехало до БД.
        using var readScope = provider.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
        var sample = await readDb.LogEvents.AsNoTracking().FirstAsync();
        Assert.Equal("AU", sample.GeoCountry);
        Assert.Equal("Sydney", sample.GeoCity);
    }

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
