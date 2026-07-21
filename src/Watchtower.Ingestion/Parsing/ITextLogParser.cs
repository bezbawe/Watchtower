using Watchtower.Ingestion.Dtos;

namespace Watchtower.Ingestion.Parsing;

public interface ITextLogParser
{
    // Разбирает одну строку лога. Возвращает null для пустой/пробельной строки.
    LogEventDto? ParseLine(string line);

    // Разбирает многострочный текст (файл): одна непустая строка = одно событие.
    IEnumerable<LogEventDto> Parse(string text);
}
