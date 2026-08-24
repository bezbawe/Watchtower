using Watchtower.Entities.Events;

namespace Watchtower.Alerting;

// Общая агрегация событий в часовые корзины для батчевой детекции по расписанию (L2 — статистика,
// L3 — ML.NET). Корзины [since; currentHour) с нулями для пустых часов — «событий/час» должно
// учитывать и тихие часы, чтобы baseline/ряд отражал реальный разброс.
public static class HourlyBucketing
{
    public static List<HourBucket> BucketCompletedHours(
        IReadOnlyList<LogEvent> windowEvents, DateTimeOffset since, DateTimeOffset currentHour)
    {
        var buckets = new SortedDictionary<DateTimeOffset, HourBucket>();
        for (var h = since; h < currentHour; h = h.AddHours(1))
            buckets[h] = new HourBucket(h);

        foreach (var e in windowEvents)
        {
            var hour = FloorToHour(e.Timestamp);
            if (buckets.TryGetValue(hour, out var bucket))
            {
                bucket.Count++;
                bucket.EventIds.Add(e.Id);
            }
        }

        return buckets.Values.ToList();
    }

    public static DateTimeOffset FloorToHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}

public sealed class HourBucket(DateTimeOffset hour)
{
    public DateTimeOffset Hour { get; } = hour;
    public int Count { get; set; }
    public List<Guid> EventIds { get; } = [];
}
