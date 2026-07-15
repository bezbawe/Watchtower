using System.ComponentModel.DataAnnotations;
using Watchtower.Entities.Enums;

namespace Watchtower.Entities.Events;

public class LogEvent : BaseEntity
{
    public DateTimeOffset Timestamp { get; set; }

    [Required]
    [StringLength(200)]
    public string Source { get; set; } = string.Empty;

    public Severity Severity { get; set; } = Severity.Info;

    public EventType EventType { get; set; } = EventType.Unknown;

    public string Message { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Actor { get; set; }

    [StringLength(45)]
    public string? SourceIp { get; set; }

    [StringLength(100)]
    public string? GeoCountry { get; set; }

    [StringLength(100)]
    public string? GeoCity { get; set; }

    // Произвольные структурированные поля исходного лога (хранятся как jsonb).
    public Dictionary<string, string> Fields { get; set; } = new();
}
