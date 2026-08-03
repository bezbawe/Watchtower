using System.Text.Json;
using Watchtower.Detection.Detectors;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Enrichment;
using Watchtower.Ingestion.Normalization;
using Watchtower.LogGenerator;

namespace Watchtower.Detection.Tests;

// Сквозная проверка Фазы 4: заложенные генератором (Фаза 3) аномалии порождают алерты с
// объяснением. Путь событий совпадает с боевым: SyntheticEvent -> JSON -> LogEventDto ->
// нормализация (+гео) -> детекция. Без БД и без живого HTTP.
public class GeneratorDetectionTests
{
    [Fact]
    public void GeneratedAnomalies_ProduceExplainedAlerts()
    {
        var options = new GeneratorOptions
        {
            Seed = 20260826,
            PeriodHours = 168,
            NormalEventsPerHour = 2,
            BruteForce = new BruteForceScenario { Enabled = true, Incidents = 1, Attempts = 10, WithinMinutes = 3 },
            OffHours = new OffHoursScenario { Enabled = true, Incidents = 2 },
            GeoAnomaly = new GeoAnomalyScenario { Enabled = true, Incidents = 1 },
        };
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

        var events = NormalizeThroughApiPath(SyntheticLogStream.Generate(options, now));

        var engine = new DetectionEngine(new IDetector[]
        {
            new BruteForceDetector(new BruteForceOptions()),
            new OffHoursDetector(new OffHoursOptions()),
            new PrivilegeEscalationDetector(new PrivilegeEscalationOptions()),
            new ImpossibleTravelDetector(new ImpossibleTravelOptions()),
        });

        var alerts = engine.Run(events);

        // Каждая заложенная аномалия дала алерт.
        var brute = Assert.Single(alerts, a => a.DetectorName == "brute_force" && a.Explanation.Contains("77.240.1.10"));
        Assert.Contains(alerts, a => a.DetectorName == "off_hours");
        Assert.Contains(alerts, a => a.DetectorName == "impossible_travel");

        // Explainability: объяснение непустое и содержит порог.
        Assert.All(alerts, a => Assert.False(string.IsNullOrWhiteSpace(a.Explanation)));
        Assert.Contains("threshold", brute.Explanation);
        Assert.True(brute.RelatedEventIds.Count >= 5);

        // Off-hours админ-действия выполняют АВТОРИЗОВАННЫЕ админы -> ложной эскалации нет.
        Assert.DoesNotContain(alerts, a => a.DetectorName == "privilege_escalation");
    }

    private static List<Entities.Events.LogEvent> NormalizeThroughApiPath(IReadOnlyList<SyntheticEvent> synthetic)
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dtos = JsonSerializer.Deserialize<List<LogEventDto>>(JsonSerializer.Serialize(synthetic, web), web)!;

        var normalizer = new LogEventNormalizer(new StubGeoIpResolver());
        return dtos.Select(d =>
        {
            var e = normalizer.Normalize(d);
            e.Id = Guid.NewGuid(); // в бою Id проставляется при записи в БД; здесь — вручную для RelatedEventIds
            return e;
        }).ToList();
    }
}
