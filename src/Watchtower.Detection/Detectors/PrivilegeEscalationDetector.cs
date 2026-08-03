using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Detection.Detectors;

// Privilege escalation: привилегированное действие актором вне списка авторизованных (MITRE T1548).
public class PrivilegeEscalationDetector(PrivilegeEscalationOptions options) : IDetector
{
    public string Name => "privilege_escalation";

    public IEnumerable<Alert> Detect(IReadOnlyCollection<LogEvent> events)
    {
        var authorized = new HashSet<string>(options.AuthorizedActors, StringComparer.OrdinalIgnoreCase);

        foreach (var e in events)
        {
            if (e.EventType != EventType.PrivilegeAction)
                continue;
            if (e.Actor is not null && authorized.Contains(e.Actor))
                continue;

            yield return new Alert
            {
                Severity = AlertSeverity.Critical,
                DetectorName = Name,
                Title = $"Privilege escalation by {e.Actor ?? "unknown"}",
                Explanation =
                    $"{e.Actor ?? "unknown"} performed a privileged action ({e.EventType}) but is not in the " +
                    $"authorized set ({string.Join(", ", options.AuthorizedActors)}).",
                MitreTechniques = ["T1548"],
                RelatedEventIds = [e.Id],
            };
        }
    }
}
