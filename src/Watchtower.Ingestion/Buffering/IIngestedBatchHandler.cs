using Watchtower.Entities.Events;

namespace Watchtower.Ingestion.Buffering;

// Хук, вызываемый после успешной записи батча событий в БД. Позволяет хосту навесить
// детекцию/алертинг на живой поток приёма, не связывая слой Ingestion с Detection.
// Ноль обработчиков = прежнее поведение (только запись событий).
public interface IIngestedBatchHandler
{
    Task HandleAsync(IReadOnlyList<LogEvent> batch, CancellationToken cancellationToken);
}
