using Watchtower.Entities.Events;
using Watchtower.Ingestion.Dtos;

namespace Watchtower.Ingestion.Normalization;

public interface ILogEventNormalizer
{
    // Приводит входящий DTO к модели события: дефолты, парсинг enum'ов, гео-обогащение.
    LogEvent Normalize(LogEventDto dto);
}
