using Watchtower.Entities.Alerts;
using Watchtower.Entities.Events;

namespace Watchtower.Detection;

// Конвейер L1: прогоняет батч событий через все зарегистрированные детекторы и собирает алерты.
public class DetectionEngine(IEnumerable<IDetector> detectors)
{
    public IReadOnlyList<Alert> Run(IReadOnlyCollection<LogEvent> events)
        => detectors.SelectMany(d => d.Detect(events)).ToList();
}
