using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Watchtower.Entities.Events;

namespace Watchtower.Ingestion.Buffering;

// Producer-consumer буфер приёма на ограниченном Channel<T>. Регистрируется как singleton:
// producer'ы (HTTP-эндпоинты) и единственный consumer (BackgroundService) делят один канал.
public class ChannelEventIngestQueue : IEventIngestQueue
{
    private readonly Channel<LogEvent> _channel;

    public ChannelEventIngestQueue(IOptions<IngestionOptions> options)
    {
        _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<LogEvent> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(logEvent, cancellationToken);
}
