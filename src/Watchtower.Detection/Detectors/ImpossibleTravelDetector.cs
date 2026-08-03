using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Detection.Detectors;

// Impossible travel: успешные логины одного пользователя из разных стран за короткое время
// (MITRE T1078). Гео уровня страны (из IGeoIpResolver); внутренние адреса игнорируются.
public class ImpossibleTravelDetector(ImpossibleTravelOptions options) : IDetector
{
    public string Name => "impossible_travel";

    public IEnumerable<Alert> Detect(IReadOnlyCollection<LogEvent> events)
    {
        var window = TimeSpan.FromMinutes(options.WindowMinutes);

        var byUser = events
            .Where(e => e.EventType == EventType.LoginSuccess && e.Actor is not null && HasRealGeo(e))
            .GroupBy(e => e.Actor!);

        foreach (var group in byUser)
        {
            var ordered = group.OrderBy(e => e.Timestamp).ToList();
            for (var i = 0; i + 1 < ordered.Count; i++)
            {
                var a = ordered[i];
                var b = ordered[i + 1];
                if (a.GeoCountry == b.GeoCountry)
                    continue;
                if (b.Timestamp - a.Timestamp > window)
                    continue;

                yield return BuildAlert(group.Key, a, b);
                break; // один алерт на пользователя
            }
        }
    }

    private static bool HasRealGeo(LogEvent e)
        => e.GeoCountry is not null && !string.Equals(e.GeoCountry, "Internal", StringComparison.OrdinalIgnoreCase);

    private Alert BuildAlert(string user, LogEvent a, LogEvent b)
    {
        var minutes = (b.Timestamp - a.Timestamp).TotalMinutes;
        return new Alert
        {
            Severity = AlertSeverity.High,
            DetectorName = Name,
            Title = $"Impossible travel for {user}",
            Explanation =
                $"{user} logged in from {a.GeoCountry} ({a.GeoCity}) and {b.GeoCountry} ({b.GeoCity}) " +
                $"within {minutes:0.#} min — physically impossible (threshold: {options.WindowMinutes} min).",
            MitreTechniques = ["T1078"],
            RelatedEventIds = [a.Id, b.Id],
        };
    }
}
