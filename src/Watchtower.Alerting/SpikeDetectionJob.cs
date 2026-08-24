using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Watchtower.Detection;
using Watchtower.Detection.MachineLearning;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting;

// L3 батчевая ML-детекция (запускается по расписанию — Hangfire). Считает число событий по часам
// за окно (та же агрегация, что и L2) и прогоняет ряд через ML.NET SSA spike detection, которая
// сама учит структуру ряда, без ручных порогов. Аномалию публикует как объяснимый алерт тем же
// путём, что L1/L2.
public class SpikeDetectionJob(
    IEventRepository events,
    SsaSpikeDetector detector,
    IAlertPublisher publisher,
    IOptions<DetectionOptions> options,
    ILogger<SpikeDetectionJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value.Ml;
        var currentHour = HourlyBucketing.FloorToHour(DateTimeOffset.UtcNow);
        var since = currentHour.AddHours(-opts.WindowHours);

        var windowEvents = await events.GetSinceAsync(since, 100_000);
        // Только завершённые часы: текущий (неполный) час исключаем.
        var buckets = HourlyBucketing.BucketCompletedHours(windowEvents, since, currentHour);

        if (buckets.Count < opts.MinBaselinePoints + 1)
        {
            logger.LogInformation(
                "L3 ML spike: not enough data ({Count} completed hours < {Min}); skipping",
                buckets.Count, opts.MinBaselinePoints + 1);
            return;
        }

        var series = buckets.Select(b => b.Count).ToList();
        var anomaly = detector.Evaluate(series);
        if (anomaly is null)
        {
            logger.LogInformation("L3 ML spike: last completed hour not flagged by SSA model");
            return;
        }

        var flagged = buckets[^1];
        var alert = BuildAlert(anomaly, flagged, opts);
        await publisher.PublishAsync([alert], cancellationToken);
        logger.LogInformation(
            "L3 ML spike at {Hour:u}: observed {Observed}, p-value={PValue:0.###}",
            flagged.Hour, anomaly.Observed, anomaly.PValue);
    }

    private static Alert BuildAlert(MlSpikeAnomaly a, HourBucket flagged, MlOptions opts)
    {
        var hourText = $"{flagged.Hour:yyyy-MM-dd HH:00} UTC";

        return new Alert
        {
            Severity = AlertSeverity.Medium,
            DetectorName = "ml_spike",
            Title = $"Activity spike (ML): {a.Observed:0} events at {hourText}",
            Explanation =
                $"ML.NET SSA spike detection flagged hour {hourText}: observed {a.Observed:0} events; " +
                $"raw score {a.RawScore:0.##}, p-value {a.PValue:0.###} (confidence {opts.Confidence:0.#}%) " +
                $"over the last {opts.WindowHours} h.",
            MitreTechniques = [],
            RelatedEventIds = flagged.EventIds.Take(100).ToList(),
            Status = AlertStatus.New,
        };
    }
}
