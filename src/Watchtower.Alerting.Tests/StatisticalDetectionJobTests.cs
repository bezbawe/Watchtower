using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Watchtower.Detection;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;
using Watchtower.Repository;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting.Tests;

// Сквозной прогон L2 на реальном PostgreSQL: сеем плоский-ish baseline по часам + резкий
// всплеск в последнем завершённом часе, гоняем job на реальных репозиториях и проверяем,
// что искусственное отклонение от baseline флагается объяснимым алертом и рассылается.
public class StatisticalDetectionJobTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Run_ArtificialSpike_FlagsStatisticalAlert_AndBroadcasts()
    {
        // Окно = 14ч, чтобы сеяные 14 завершённых часов заполнили baseline без «нулевых» дыр.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Detection:Statistical:WindowHours"] = "14",
                ["Detection:Statistical:MinBaselinePoints"] = "12",
                ["Detection:Statistical:ZScoreThreshold"] = "3",
            })
            .Build();

        var broadcaster = new CapturingBroadcaster();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWatchtowerDbContext(_container.GetConnectionString());
        services.AddWatchtowerRepositories();
        services.AddWatchtowerDetection(configuration);
        services.AddWatchtowerAlerting(configuration);
        services.AddSingleton<IAlertBroadcaster>(broadcaster);

        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>().Database.MigrateAsync();

        await SeedAsync(provider);

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<StatisticalDetectionJob>().RunAsync(CancellationToken.None);

        using var readScope = provider.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
        var alert = await db.Alerts.AsNoTracking().FirstOrDefaultAsync(a => a.DetectorName == "statistical_volume");

        Assert.NotNull(alert);
        Assert.Contains("z-score", alert!.Explanation);
        Assert.Contains("spike", alert.Explanation);
        Assert.Contains(broadcaster.Received, n => n.DetectorName == "statistical_volume");
    }

    private static async Task SeedAsync(IServiceProvider provider)
    {
        var currentHour = FloorToHour(DateTimeOffset.UtcNow);
        // Часовые счётчики для offsets 14..2 (13 baseline-часов) — ровно, но с разбросом.
        var baselineCounts = new[] { 38, 41, 39, 42, 40, 37, 43, 40, 39, 41, 38, 42, 40 };

        var toSeed = new List<LogEvent>();
        for (var idx = 0; idx < baselineCounts.Length; idx++)
        {
            var hour = currentHour.AddHours(-(14 - idx)); // 14, 13, ..., 2
            AddEvents(toSeed, hour, baselineCounts[idx]);
        }

        // Резкий всплеск в последнем завершённом часе (offset 1).
        AddEvents(toSeed, currentHour.AddHours(-1), 200);

        using var scope = provider.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        await events.AddRangeAsync(toSeed);
    }

    private static void AddEvents(List<LogEvent> sink, DateTimeOffset hour, int count)
    {
        for (var i = 0; i < count; i++)
            sink.Add(new LogEvent
            {
                Timestamp = hour.AddMinutes(i % 60),
                EventType = EventType.LoginSuccess,
                Source = "app",
                Message = "activity",
            });
    }

    private static DateTimeOffset FloorToHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
