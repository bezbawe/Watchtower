using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Alerting.Tests;

internal static class AlertingTestData
{
    private static readonly DateTimeOffset Base = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    // count неудачных логинов с одного IP в пределах ~пары минут (порог brute-force по умолчанию = 5/15мин).
    public static List<LogEvent> BruteForceBatch(string ip, int count) =>
        Enumerable.Range(0, count)
            .Select(i => new LogEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = Base.AddSeconds(i * 30),
                EventType = EventType.LoginFailed,
                Source = "auth-service",
                Actor = "victim",
                SourceIp = ip,
                Message = $"failed login {i}",
            })
            .ToList();
}
