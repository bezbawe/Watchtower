using Watchtower.Entities.Alerts;

namespace Watchtower.Alerting;

// Полезная нагрузка алерта для live-канала (SignalR) и внешних уведомлений — плоская,
// чтобы не гонять по проводу EF-сущность.
public record AlertNotification(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Severity,
    string DetectorName,
    string Title,
    string Explanation,
    IReadOnlyList<string> MitreTechniques,
    string Status)
{
    public static AlertNotification FromAlert(Alert alert) => new(
        alert.Id,
        alert.CreatedAt,
        alert.Severity.ToString(),
        alert.DetectorName,
        alert.Title,
        alert.Explanation,
        alert.MitreTechniques,
        alert.Status.ToString());
}
