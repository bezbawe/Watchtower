using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Watchtower.Entities.Events;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Ingestion.Buffering;

// Consumer: вычитывает события из буфера и пишет их в БД батчами (AddRangeAsync).
// Каждый батч пишется в своём DI-scope, т.к. репозиторий/DbContext — scoped.
public class EventIngestBackgroundService(
    IEventIngestQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    ILogger<EventIngestBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batchSize = options.Value.BatchSize;
        var reader = queue.Reader;
        var batch = new List<LogEvent>(batchSize);

        logger.LogInformation("Event ingest consumer started");

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < batchSize && reader.TryRead(out var logEvent))
                    batch.Add(logEvent);

                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка сервиса.
        }

        // Финальный слив остатков буфера при завершении.
        while (reader.TryRead(out var logEvent))
        {
            batch.Add(logEvent);
            if (batch.Count >= batchSize)
            {
                await WriteBatchAsync(batch);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            await WriteBatchAsync(batch);
    }

    private async Task WriteBatchAsync(List<LogEvent> batch)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            await repository.AddRangeAsync(batch);
            logger.LogInformation("Ingested batch of {Count} events", batch.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist batch of {Count} events", batch.Count);
        }
    }
}
