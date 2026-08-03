using Watchtower.Detection.Detectors;
using Watchtower.Entities.Enums;

namespace Watchtower.Detection.Tests;

public class OffHoursDetectorTests
{
    private static readonly OffHoursOptions Options = new();

    private static readonly DateTimeOffset WeekdayNight = new(2026, 8, 26, 3, 0, 0, TimeSpan.Zero);   // Wed 03:00
    private static readonly DateTimeOffset WeekdayNoon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);   // Wed 12:00

    [Fact]
    public void Fires_When_SensitiveAction_OutsideBusinessHours()
    {
        var events = new[] { TestEvents.Event(EventType.PrivilegeAction, WeekdayNight, actor: "admin") };

        var alert = Assert.Single(new OffHoursDetector(Options).Detect(events).ToList());
        Assert.Equal("off_hours", alert.DetectorName);
        Assert.Equal(AlertSeverity.Medium, alert.Severity);
    }

    [Fact]
    public void DoesNotFire_During_BusinessHours()
    {
        var events = new[] { TestEvents.Event(EventType.PrivilegeAction, WeekdayNoon, actor: "admin") };

        Assert.Empty(new OffHoursDetector(Options).Detect(events).ToList());
    }

    [Fact]
    public void DoesNotFire_For_NonSensitiveEvent_OffHours()
    {
        var events = new[] { TestEvents.Event(EventType.LoginSuccess, WeekdayNight, actor: "alice") };

        Assert.Empty(new OffHoursDetector(Options).Detect(events).ToList());
    }
}
