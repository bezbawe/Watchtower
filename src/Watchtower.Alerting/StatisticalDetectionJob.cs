using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Watchtower.Detection;
using Watchtower.Detection.Statistics;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting;

// L2 батчевая статистическая детекция (запускается по расписанию — Hangfire). Считает число
// событий по часам за окно, строит baseline из прошлых часов и оценивает последний завершённый
// час на отклонение (z-score). Аномалию публикует как объяснимый алерт тем же путём, что и L1.
public class StatisticalDetectionJob(
    IEventRepository events,
    StatisticalAnomalyDetector detector,
    IAlertPublisher publisher,
    IOptions<DetectionOptions> options,
    ILogger<StatisticalDetectionJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value.Statistical;
        var currentHour = FloorToHour(DateTimeOffset.UtcNow);
        var since = currentHour.AddHours(-opts.WindowHours);

        var windowEvents = await events.GetSinceAsync(since, 100_000);
        // Только завершённые часы: текущий (неполный) час исключаем.
        var buckets = BucketCompletedHours(windowEvents, since, currentHour);

        if (buckets.Count < opts.MinBaselinePoints + 1)
        {
            logger.LogInformation(
                "L2 statistical: not enough baseline ({Count} completed hours < {Min}); skipping",
                buckets.Count, opts.MinBaselinePoints + 1);
            return;
        }

        var series = buckets.Select(b => b.Count).ToList();
        var anomaly = detector.Evaluate(series);
        if (anomaly is null)
        {
            logger.LogInformation("L2 statistical: last completed hour within baseline; no anomaly");
            return;
        }

        var flagged = buckets[^1];
        var alert = BuildAlert(anomaly, flagged, opts);
        await publisher.PublishAsync([alert], cancellationToken);
        logger.LogInformation(
            "L2 statistical anomaly at {Hour:u}: observed {Observed}, z={Z:0.##}",
            flagged.Hour, anomaly.Observed, anomaly.ZScore);
    }

    private static Alert BuildAlert(StatisticalAnomaly a, HourBucket flagged, StatisticalOptions opts)
    {
        var direction = a.IsSpike ? "spike" : "drop";
        var hourText = $"{flagged.Hour:yyyy-MM-dd HH:00} UTC";

        return new Alert
        {
            Severity = AlertSeverity.Medium,
            DetectorName = "statistical_volume",
            Title = $"Activity {direction}: {a.Observed:0} events at {hourText}",
            Explanation =
                $"Hourly event volume {direction}: observed {a.Observed:0} in hour {hourText} vs baseline " +
                $"mean {a.BaselineMean:0.#} ± {a.BaselineStdDev:0.#} (EWMA {a.Ewma:0.#}); " +
                $"z-score {a.ZScore:0.##} (threshold {opts.ZScoreThreshold:0.#}) over the last {opts.WindowHours} h.",
            MitreTechniques = [],
            RelatedEventIds = flagged.EventIds.Take(100).ToList(),
            Status = AlertStatus.New,
        };
    }

    // Часовые корзины [since; currentHour) с нулями для пустых часов — «событий/час» должно
    // учитывать и тихие часы, чтобы baseline отражал реальный разброс.
    private static List<HourBucket> BucketCompletedHours(
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

    private static DateTimeOffset FloorToHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    private sealed class HourBucket(DateTimeOffset hour)
    {
        public DateTimeOffset Hour { get; } = hour;
        public int Count { get; set; }
        public List<Guid> EventIds { get; } = [];
    }
}
