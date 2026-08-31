using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Detection.Detectors;

// Correlation rule: детекция по цепочке событий, а не по одиночному событию — неудачный логин
// -> успешный логин с того же IP -> тот же актор обращается к данным, всё в пределах короткого
// окна (MITRE T1110 + T1005). Алерт хранит всю цепочку в RelatedEventIds.
public class AccountCompromiseChainDetector(CorrelationOptions options) : IDetector
{
    public string Name => "account_compromise_chain";

    public IEnumerable<Alert> Detect(IReadOnlyCollection<LogEvent> events)
    {
        var window = TimeSpan.FromMinutes(options.WindowMinutes);
        var ordered = events.OrderBy(e => e.Timestamp).ToList();

        var failedByIp = ordered
            .Where(e => e.EventType == EventType.LoginFailed && e.SourceIp is not null)
            .GroupBy(e => e.SourceIp!);

        foreach (var group in failedByIp)
        {
            var fail = group.First(); // самый ранний неудачный логин с этого IP в батче
            var success = ordered.FirstOrDefault(e =>
                e.EventType == EventType.LoginSuccess &&
                e.SourceIp == fail.SourceIp &&
                e.Actor is not null &&
                e.Timestamp > fail.Timestamp &&
                e.Timestamp - fail.Timestamp <= window);
            if (success is null)
                continue;

            var dataAccess = ordered.FirstOrDefault(e =>
                e.EventType == EventType.DataAccess &&
                e.Actor == success.Actor &&
                e.Timestamp > success.Timestamp &&
                e.Timestamp - success.Timestamp <= window);
            if (dataAccess is null)
                continue;

            yield return BuildAlert(fail, success, dataAccess);
        }
    }

    private Alert BuildAlert(LogEvent fail, LogEvent success, LogEvent dataAccess) => new()
    {
        Severity = AlertSeverity.Critical,
        DetectorName = Name,
        Title = $"Account compromise chain: {success.Actor} via {fail.SourceIp}",
        Explanation =
            $"Failed login from {fail.SourceIp} at {fail.Timestamp:HH:mm:ss} was followed by a successful " +
            $"login as {success.Actor} from the same IP at {success.Timestamp:HH:mm:ss}, then {success.Actor} " +
            $"accessed data at {dataAccess.Timestamp:HH:mm:ss} — all within {options.WindowMinutes} min.",
        MitreTechniques = ["T1110", "T1005"],
        RelatedEventIds = [fail.Id, success.Id, dataAccess.Id],
    };
}
