using Microsoft.Extensions.DependencyInjection;
using Watchtower.Alerting;
using Watchtower.Entities.Events;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Web.Services;

// Данные дашборда. Каждый вызов работает в собственном DI-scope, чтобы не держать
// scoped DbContext на всё время жизни Blazor-цепи (circuit).
public class DashboardService(IServiceScopeFactory scopeFactory)
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(int feedLimit, int alertLimit, int windowHours)
    {
        using var scope = scopeFactory.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertRepository>();

        var since = DateTimeOffset.UtcNow.AddHours(-windowHours);
        var windowEvents = await events.GetSinceAsync(since, 10_000);

        var recent = await events.GetRecentAsync(feedLimit);
        var active = await alerts.GetActiveAsync(alertLimit);
        var totalEvents = await events.GetCountAsync();
        var totalAlerts = await alerts.GetCountAsync();

        var activity = BucketByHour(windowEvents, windowHours);
        var topTypes = windowEvents
            .GroupBy(e => e.EventType)
            .Select(g => new EventTypeCount(g.Key.ToString(), g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        return new DashboardSnapshot(
            active.Select(AlertNotification.FromAlert).ToList(),
            recent,
            activity,
            topTypes,
            totalEvents,
            totalAlerts);
    }

    // Часовые корзины за последние windowHours (UTC), включая пустые часы — чтобы график был ровным.
    private static List<HourBucket> BucketByHour(IReadOnlyList<LogEvent> events, int windowHours)
    {
        var nowHour = FloorToHour(DateTimeOffset.UtcNow);
        var buckets = Enumerable.Range(0, windowHours + 1)
            .ToDictionary(i => nowHour.AddHours(-windowHours + i), _ => 0);

        foreach (var e in events)
        {
            var key = FloorToHour(e.Timestamp);
            if (buckets.ContainsKey(key))
                buckets[key]++;
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new HourBucket(kv.Key, kv.Value))
            .ToList();
    }

    private static DateTimeOffset FloorToHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}

public record DashboardSnapshot(
    IReadOnlyList<AlertNotification> ActiveAlerts,
    IReadOnlyList<LogEvent> RecentEvents,
    IReadOnlyList<HourBucket> Activity,
    IReadOnlyList<EventTypeCount> TopEventTypes,
    int TotalEvents,
    int TotalAlerts);

public record HourBucket(DateTimeOffset Hour, int Count);

public record EventTypeCount(string EventType, int Count);
