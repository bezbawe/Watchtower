using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Detection.Detectors;

// Brute-force: >= Threshold неудачных логинов с одного IP за WindowMinutes (MITRE T1110).
public class BruteForceDetector(BruteForceOptions options) : IDetector
{
    public string Name => "brute_force";

    public IEnumerable<Alert> Detect(IReadOnlyCollection<LogEvent> events)
    {
        var window = TimeSpan.FromMinutes(options.WindowMinutes);

        var byIp = events
            .Where(e => e.EventType == EventType.LoginFailed && e.SourceIp is not null)
            .GroupBy(e => e.SourceIp!);

        foreach (var group in byIp)
        {
            var ordered = group.OrderBy(e => e.Timestamp).ToList();

            // Скользящее окно: первое окно, где неудач набирается >= порога.
            var start = 0;
            for (var end = 0; end < ordered.Count; end++)
            {
                while (ordered[end].Timestamp - ordered[start].Timestamp > window)
                    start++;

                var count = end - start + 1;
                if (count < options.Threshold)
                    continue;

                yield return BuildAlert(group.Key, ordered.GetRange(start, count));
                break; // один алерт на IP
            }
        }
    }

    private Alert BuildAlert(string ip, List<LogEvent> windowEvents)
    {
        var span = windowEvents[^1].Timestamp - windowEvents[0].Timestamp;
        var targets = windowEvents
            .Where(e => e.Actor is not null)
            .Select(e => e.Actor!)
            .Distinct()
            .ToList();
        var targetText = targets.Count > 0 ? string.Join(", ", targets) : "unknown";

        return new Alert
        {
            Severity = AlertSeverity.High,
            DetectorName = Name,
            Title = $"Brute-force: {windowEvents.Count} failed logins from {ip}",
            Explanation =
                $"{windowEvents.Count} failed logins from {ip} within {span.TotalMinutes:0.#} min " +
                $"(threshold: {options.Threshold} within {options.WindowMinutes} min). " +
                $"Targeted account(s): {targetText}.",
            MitreTechniques = ["T1110"],
            RelatedEventIds = windowEvents.Select(e => e.Id).ToList(),
        };
    }
}
