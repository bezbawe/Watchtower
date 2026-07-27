using System.Text.Json;
using Watchtower.Ingestion.Dtos;
using Watchtower.LogGenerator;

namespace Watchtower.LogGenerator.Tests;

// Unit-тесты на чистый движок: поток детерминирован и содержит заданные аномалии
// (brute-force всплеск, off-hours, geo-аномалия) — без живого API и БД.
public class SyntheticLogStreamTests
{
    // Фиксированное «сейчас» — среда, полдень UTC; период с запасом будних дней.
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static GeneratorOptions TestOptions() => new()
    {
        Seed = 42,
        PeriodHours = 168,
        NormalEventsPerHour = 2,
        BruteForce = new BruteForceScenario { Enabled = true, Incidents = 2, Attempts = 10, WithinMinutes = 3 },
        OffHours = new OffHoursScenario { Enabled = true, Incidents = 3 },
        GeoAnomaly = new GeoAnomalyScenario { Enabled = true, Incidents = 2 },
    };

    [Fact]
    public void Generate_ProducesExpectedTotals_AndIsDeterministic()
    {
        var options = TestOptions();

        var expectedNormal = options.PeriodHours * options.NormalEventsPerHour;
        var expectedBrute = options.BruteForce.Incidents * (options.BruteForce.Attempts + 1); // + пробитие
        var expectedOff = options.OffHours.Incidents;
        var expectedGeo = options.GeoAnomaly.Incidents * 2;
        var expectedTotal = expectedNormal + expectedBrute + expectedOff + expectedGeo;

        var first = SyntheticLogStream.Generate(options, Now);
        var second = SyntheticLogStream.Generate(options, Now);

        Assert.Equal(expectedTotal, first.Count);
        // Один и тот же сид + время -> побайтово одинаковый поток.
        Assert.Equal(Serialize(first), Serialize(second));
        // Отсортировано по времени.
        Assert.True(first.SequenceEqual(first.OrderBy(e => e.Timestamp)));
    }

    [Fact]
    public void Generate_ContainsBruteForceSpike_FromSingleIp_WithinWindow()
    {
        var options = TestOptions();
        var events = SyntheticLogStream.Generate(options, Now);

        var byIp = events
            .Where(e => Scenario(e) == "brute_force" && e.EventType == "login_failed")
            .GroupBy(e => e.SourceIp)
            .ToList();

        var spike = byIp.FirstOrDefault(g => g.Count() >= options.BruteForce.Attempts);
        Assert.NotNull(spike);

        var span = spike!.Max(e => e.Timestamp) - spike.Min(e => e.Timestamp);
        Assert.True(span <= TimeSpan.FromMinutes(options.BruteForce.WithinMinutes),
            $"brute-force spread {span} exceeds window {options.BruteForce.WithinMinutes}m");
    }

    [Fact]
    public void Generate_ContainsOffHoursPrivilegedActivity()
    {
        var events = SyntheticLogStream.Generate(TestOptions(), Now);

        var offHours = events.Where(e => Scenario(e) == "off_hours").ToList();

        Assert.NotEmpty(offHours);
        Assert.All(offHours, e =>
        {
            Assert.InRange(e.Timestamp.Hour, 1, 5);
            Assert.Contains(e.EventType, new[] { "privilege_action", "config_change" });
        });
    }

    [Fact]
    public void Generate_ContainsGeoAnomaly_SameUserTwoDistantIps_ShortInterval()
    {
        var events = SyntheticLogStream.Generate(TestOptions(), Now);

        var impossibleTravel = events
            .Where(e => Scenario(e) == "geo_anomaly")
            .GroupBy(e => e.Actor)
            .Any(g =>
            {
                // Два соседних по времени логина одного пользователя с разных IP за < 1 часа.
                var ordered = g.OrderBy(e => e.Timestamp).ToList();
                for (var i = 0; i + 1 < ordered.Count; i++)
                    if (ordered[i].SourceIp != ordered[i + 1].SourceIp
                        && ordered[i + 1].Timestamp - ordered[i].Timestamp <= TimeSpan.FromHours(1))
                        return true;
                return false;
            });

        Assert.True(impossibleTravel, "expected a user logging in from two distinct IPs within an hour");
    }

    [Fact]
    public void SyntheticEvent_JsonShape_MatchesLogEventDto()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var sample = new SyntheticEvent
        {
            Timestamp = Now,
            Source = "auth-service",
            Severity = "Warning",
            EventType = "login_failed",
            Message = "failed password",
            Actor = "alice",
            SourceIp = "77.240.1.10",
            Fields = new Dictionary<string, string> { ["scenario"] = "brute_force", ["attempt"] = "3" },
        };

        // То, что уходит по проводу, должно десериализоваться в серверный DTO без потерь.
        var json = JsonSerializer.Serialize(sample, web);
        var dto = JsonSerializer.Deserialize<LogEventDto>(json, web);

        Assert.NotNull(dto);
        Assert.Equal(sample.Timestamp, dto!.Timestamp);
        Assert.Equal(sample.Source, dto.Source);
        Assert.Equal(sample.Severity, dto.Severity);
        Assert.Equal(sample.EventType, dto.EventType);
        Assert.Equal(sample.Message, dto.Message);
        Assert.Equal(sample.Actor, dto.Actor);
        Assert.Equal(sample.SourceIp, dto.SourceIp);
        Assert.Equal(sample.Fields, dto.Fields);
    }

    private static string? Scenario(SyntheticEvent e) =>
        e.Fields.TryGetValue("scenario", out var s) ? s : null;

    private static string Serialize(IEnumerable<SyntheticEvent> events) =>
        JsonSerializer.Serialize(events);
}
