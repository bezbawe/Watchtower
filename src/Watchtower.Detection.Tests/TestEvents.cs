using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;

namespace Watchtower.Detection.Tests;

internal static class TestEvents
{
    // Базовое будничное «сейчас» в UTC (среда, полдень) — вне выходных и вне ночи.
    public static readonly DateTimeOffset Weekday = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    public static LogEvent Event(
        EventType type,
        DateTimeOffset timestamp,
        string? actor = null,
        string? sourceIp = null,
        string? geoCountry = null,
        string? geoCity = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = type,
            Actor = actor,
            SourceIp = sourceIp,
            GeoCountry = geoCountry,
            GeoCity = geoCity,
        };
}
