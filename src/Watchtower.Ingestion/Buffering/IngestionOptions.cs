namespace Watchtower.Ingestion.Buffering;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    // Максимум событий в буфере приёма; при переполнении producer ждёт (backpressure).
    public int QueueCapacity { get; set; } = 10_000;

    // Максимальный размер одной батч-записи в БД.
    public int BatchSize { get; set; } = 500;
}
