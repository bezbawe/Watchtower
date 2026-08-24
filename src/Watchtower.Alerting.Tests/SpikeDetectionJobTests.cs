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

// Сквозной прогон L3 на реальном PostgreSQL: сеем ровный (без сезонности) фон по часам + резкий
// всплеск в последнем завершённом часе, гоняем job на реальных репозиториях и проверяем, что
// ML.NET SSA spike detection флагает искусственный всплеск объяснимым алертом и рассылает его.
public class SpikeDetectionJobTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Run_ArtificialSpike_FlagsMlAlert_AndBroadcasts()
    {
        // Окно = 21ч: 20 baseline-часов (MinBaselinePoints) + 1 оцениваемый.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Detection:Ml:WindowHours"] = "21",
                ["Detection:Ml:MinBaselinePoints"] = "20",
                ["Detection:Ml:Confidence"] = "95",
                ["Detection:Ml:PValueHistoryLength"] = "8",
                ["Detection:Ml:SeasonalityWindowSize"] = "8",
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
            await scope.ServiceProvider.GetRequiredService<SpikeDetectionJob>().RunAsync(CancellationToken.None);

        using var readScope = provider.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
        var alert = await db.Alerts.AsNoTracking().FirstOrDefaultAsync(a => a.DetectorName == "ml_spike");

        Assert.NotNull(alert);
        Assert.Contains("SSA", alert!.Explanation);
        Assert.Contains(broadcaster.Received, n => n.DetectorName == "ml_spike");
    }

    private static async Task SeedAsync(IServiceProvider provider)
    {
        var currentHour = FloorToHour(DateTimeOffset.UtcNow);
        // Ровный (но не плоский) фон без сезонности — 20 baseline-часов.
        var baselineCounts = new[] { 38, 41, 39, 42, 40, 37, 43, 40, 39, 41, 38, 42, 40, 39, 41, 40, 38, 42, 39, 40 };

        var toSeed = new List<LogEvent>();
        for (var idx = 0; idx < baselineCounts.Length; idx++)
        {
            var hour = currentHour.AddHours(-(21 - idx)); // 21, 20, ..., 2
            AddEvents(toSeed, hour, baselineCounts[idx]);
        }

        // Резкий всплеск в последнем завершённом часе (offset 1).
        AddEvents(toSeed, currentHour.AddHours(-1), 300);

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
