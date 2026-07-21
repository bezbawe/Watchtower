using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Enrichment;

namespace Watchtower.Ingestion.Normalization;

public class LogEventNormalizer(IGeoIpResolver geoResolver) : ILogEventNormalizer
{
    public LogEvent Normalize(LogEventDto dto)
    {
        var sourceIp = Clean(dto.SourceIp, 45);

        var ev = new LogEvent
        {
            Timestamp = (dto.Timestamp ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            Source = Clean(dto.Source, 200) ?? "unknown",
            Severity = ParseSeverity(dto.Severity),
            EventType = ParseEventType(dto.EventType),
            Message = dto.Message ?? string.Empty,
            Actor = Clean(dto.Actor, 200),
            SourceIp = sourceIp,
            Fields = dto.Fields is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(dto.Fields),
        };

        if (sourceIp is not null && geoResolver.TryResolve(sourceIp, out var geo))
        {
            ev.GeoCountry = Clean(geo.Country, 100);
            ev.GeoCity = Clean(geo.City, 100);
        }

        return ev;
    }

    private static Severity ParseSeverity(string? value)
        => Enum.TryParse<Severity>(value?.Trim(), ignoreCase: true, out var s) ? s : Severity.Info;

    private static EventType ParseEventType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EventType.Unknown;

        // Принимаем snake_case ("login_failed") и kebab-case — сравниваем без разделителей.
        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty).Trim();
        return Enum.TryParse<EventType>(normalized, ignoreCase: true, out var t) ? t : EventType.Unknown;
    }

    // Обрезает пробелы и ограничивает длину под схему БД; пустую строку превращает в null.
    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
