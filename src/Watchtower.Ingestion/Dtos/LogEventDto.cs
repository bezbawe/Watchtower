namespace Watchtower.Ingestion.Dtos;

// Входящее событие «как пришло» (JSON API или текстовый парсер).
// Все поля опциональны/строковые — валидация и приведение к модели в LogEventNormalizer.
public record LogEventDto
{
    public DateTimeOffset? Timestamp { get; init; }
    public string? Source { get; init; }
    public string? Severity { get; init; }
    public string? EventType { get; init; }
    public string? Message { get; init; }
    public string? Actor { get; init; }
    public string? SourceIp { get; init; }
    public Dictionary<string, string>? Fields { get; init; }
}
