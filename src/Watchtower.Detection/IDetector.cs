using Watchtower.Entities.Alerts;
using Watchtower.Entities.Events;

namespace Watchtower.Detection;

// L1-детектор: получает батч событий и возвращает алерты по своему правилу.
public interface IDetector
{
    string Name { get; }

    IEnumerable<Alert> Detect(IReadOnlyCollection<LogEvent> events);
}
