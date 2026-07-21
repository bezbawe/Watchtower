using System.Threading.Channels;
using Watchtower.Entities.Events;

namespace Watchtower.Ingestion.Buffering;

public interface IEventIngestQueue
{
    // Кладёт нормализованное событие в буфер (producer). Ждёт, если буфер переполнен.
    ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default);

    // Сторона чтения для consumer'а (BackgroundService).
    ChannelReader<LogEvent> Reader { get; }
}
