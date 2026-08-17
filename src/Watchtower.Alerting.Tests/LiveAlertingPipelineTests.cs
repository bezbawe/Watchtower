using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Watchtower.Detection;
using Watchtower.Entities.Alerts;
using Watchtower.Ingestion;
using Watchtower.Ingestion.Buffering;
using Watchtower.Repository;

namespace Watchtower.Alerting.Tests;

// Сквозной прогон живого алертинга на реальном PostgreSQL: реальные DI-регистрации
// (DbContext + Repositories + Ingestion + Detection + Alerting) → Channel-буфер →
// BackgroundService → пост-обработка (детекция+персист+broadcast). Без HTTP и SignalR
// (broadcaster подменён на захватывающий), т.к. проверяем именно конвейер «событие → алерт».
public class LiveAlertingPipelineTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task BruteForceBatch_ThroughPipeline_PersistsAlert_AndBroadcasts()
    {
        var configuration = new ConfigurationBuilder().Build();
        var broadcaster = new CapturingBroadcaster();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWatchtowerDbContext(_container.GetConnectionString());
        services.AddWatchtowerRepositories();
        services.AddWatchtowerIngestion(configuration);
        services.AddWatchtowerDetection(configuration);
        services.AddWatchtowerAlerting(configuration);
        services.AddSingleton<IAlertBroadcaster>(broadcaster);

        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>().Database.MigrateAsync();

        // Кладём brute-force батч В КАНАЛ ДО старта consumer'а — тогда он сольёт всё одним
        // батчем, и детектор (работающий в пределах батча) гарантированно сработает.
        var queue = provider.GetRequiredService<IEventIngestQueue>();
        foreach (var e in AlertingTestData.BruteForceBatch("203.0.113.9", count: 6))
            await queue.EnqueueAsync(e);

        var hosted = provider.GetServices<IHostedService>().ToList();
        foreach (var h in hosted)
            await h.StartAsync(CancellationToken.None);

        var alert = await WaitForAlertAsync(provider, TimeSpan.FromSeconds(20));

        foreach (var h in hosted)
            await h.StopAsync(CancellationToken.None);

        Assert.NotNull(alert);
        Assert.Equal("brute_force", alert!.DetectorName);
        Assert.Contains("threshold", alert.Explanation);
        Assert.Contains(broadcaster.Received, n => n.DetectorName == "brute_force");
    }

    private static async Task<Alert?> WaitForAlertAsync(IServiceProvider provider, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
            var alert = await db.Alerts.AsNoTracking().FirstOrDefaultAsync();
            if (alert is not null)
                return alert;
            await Task.Delay(200);
        }

        return null;
    }
}
