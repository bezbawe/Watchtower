namespace Watchtower.LogGenerator;

// Полезная нагрузка, которую генератор шлёт в POST /api/events. Форма совпадает с
// Watchtower.Ingestion.Dtos.LogEventDto (сериализуется web-дефолтами -> camelCase),
// но генератор остаётся самостоятельным клиентом API и не тянет серверные сборки.
public record SyntheticEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
    public string EventType { get; init; } = "unknown";
    public string Message { get; init; } = string.Empty;
    public string? Actor { get; init; }
    public string? SourceIp { get; init; }
    public Dictionary<string, string> Fields { get; init; } = new();
}
