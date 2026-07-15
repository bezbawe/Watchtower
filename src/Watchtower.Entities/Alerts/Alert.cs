using System.ComponentModel.DataAnnotations;
using Watchtower.Entities.Enums;

namespace Watchtower.Entities.Alerts;

public class Alert : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AlertSeverity Severity { get; set; }

    [Required]
    [StringLength(200)]
    public string DetectorName { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    // Почему сработал алерт: правило/детектор, данные, пороги.
    public string Explanation { get; set; } = string.Empty;

    // Техники MITRE ATT&CK (напр. "T1110"). Хранится как text[].
    public List<string> MitreTechniques { get; set; } = new();

    // События-основания (для correlation — вся цепочка). Хранится как uuid[].
    public List<Guid> RelatedEventIds { get; set; } = new();

    public AlertStatus Status { get; set; } = AlertStatus.New;
}
