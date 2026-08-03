using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Detection.Detectors;

// Off-hours: чувствительное действие вне рабочих часов или в выходной (MITRE T1078).
public class OffHoursDetector(OffHoursOptions options) : IDetector
{
    public string Name => "off_hours";

    public IEnumerable<Alert> Detect(IReadOnlyCollection<LogEvent> events)
    {
        foreach (var e in events)
        {
            if (!options.SensitiveEventTypes.Contains(e.EventType))
                continue;
            if (!IsOffHours(e.Timestamp))
                continue;

            yield return new Alert
            {
                Severity = AlertSeverity.Medium,
                DetectorName = Name,
                Title = $"Off-hours {e.EventType} by {e.Actor ?? "unknown"}",
                Explanation =
                    $"{e.Actor ?? "unknown"} performed {e.EventType} at {e.Timestamp:yyyy-MM-dd HH:mm} UTC " +
                    $"({e.Timestamp.DayOfWeek}), outside business hours " +
                    $"{options.BusinessStartHour:00}:00–{options.BusinessEndHour:00}:00 (Mon–Fri).",
                MitreTechniques = ["T1078"],
                RelatedEventIds = [e.Id],
            };
        }
    }

    private bool IsOffHours(DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return true;

        return utc.Hour < options.BusinessStartHour || utc.Hour >= options.BusinessEndHour;
    }
}
