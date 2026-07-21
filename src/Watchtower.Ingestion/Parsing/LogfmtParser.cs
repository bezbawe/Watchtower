using System.Globalization;
using System.Text;
using Watchtower.Ingestion.Dtos;

namespace Watchtower.Ingestion.Parsing;

// Парсер простого текстового формата logfmt: одна строка = одно событие.
// Формат: необязательный ведущий bare-токен с временной меткой, затем пары key=value
// (значение можно взять в двойные кавычки, чтобы допустить пробелы). Порядок ключей
// не важен; известные ключи мапятся на поля события, неизвестные — уходят в Fields.
// Пример: 2026-08-30T12:00:00Z source=auth severity=warning type=login_failed
//         actor=alice ip=203.0.113.7 msg="Failed login for user alice" attempt=3
public class LogfmtParser : ITextLogParser
{
    private const DateTimeStyles UtcStyles =
        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

    public LogEventDto? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        DateTimeOffset? timestamp = null;
        string? source = null, severity = null, eventType = null, message = null, actor = null, ip = null;
        Dictionary<string, string>? fields = null;
        var firstBareToken = true;

        foreach (var (key, value) in Tokenize(line))
        {
            if (key is null)
            {
                // Ведущий bare-токен трактуем как временную метку, если он парсится как дата.
                if (firstBareToken && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, UtcStyles, out var bareTs))
                    timestamp = bareTs;
                firstBareToken = false;
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "ts":
                case "time":
                case "timestamp":
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, UtcStyles, out var ts))
                        timestamp = ts;
                    break;
                case "source":
                    source = value;
                    break;
                case "severity":
                case "level":
                    severity = value;
                    break;
                case "type":
                case "eventtype":
                case "event_type":
                    eventType = value;
                    break;
                case "actor":
                case "user":
                    actor = value;
                    break;
                case "ip":
                case "sourceip":
                case "source_ip":
                    ip = value;
                    break;
                case "msg":
                case "message":
                    message = value;
                    break;
                default:
                    (fields ??= new Dictionary<string, string>())[key] = value;
                    break;
            }
        }

        return new LogEventDto
        {
            Timestamp = timestamp,
            Source = source,
            Severity = severity,
            EventType = eventType,
            Message = message,
            Actor = actor,
            SourceIp = ip,
            Fields = fields,
        };
    }

    public IEnumerable<LogEventDto> Parse(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var dto = ParseLine(line);
            if (dto is not null)
                yield return dto;
        }
    }

    // Разбивает строку на токены: (key, value) для пар key=value и (null, token) для bare-токенов.
    // Значение после '=' может быть заключено в двойные кавычки и содержать пробелы (\" — экранирование).
    private static IEnumerable<(string? Key, string Value)> Tokenize(string line)
    {
        var i = 0;
        var n = line.Length;

        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(line[i]))
                i++;
            if (i >= n)
                break;

            var start = i;
            while (i < n && line[i] != '=' && !char.IsWhiteSpace(line[i]))
                i++;

            if (i < n && line[i] == '=')
            {
                var key = line[start..i];
                i++; // пропускаем '='
                string value;

                if (i < n && line[i] == '"')
                {
                    i++; // пропускаем открывающую кавычку
                    var sb = new StringBuilder();
                    while (i < n && line[i] != '"')
                    {
                        if (line[i] == '\\' && i + 1 < n)
                            i++; // экранированный символ — берём следующий как есть
                        sb.Append(line[i]);
                        i++;
                    }
                    if (i < n)
                        i++; // пропускаем закрывающую кавычку
                    value = sb.ToString();
                }
                else
                {
                    var valueStart = i;
                    while (i < n && !char.IsWhiteSpace(line[i]))
                        i++;
                    value = line[valueStart..i];
                }

                yield return (key, value);
            }
            else
            {
                yield return (null, line[start..i]);
            }
        }
    }
}
