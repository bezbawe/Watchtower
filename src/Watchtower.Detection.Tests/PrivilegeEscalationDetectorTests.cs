using Watchtower.Detection.Detectors;
using Watchtower.Entities.Enums;

namespace Watchtower.Detection.Tests;

public class PrivilegeEscalationDetectorTests
{
    private static readonly PrivilegeEscalationOptions Options = new();

    [Fact]
    public void Fires_When_UnauthorizedActor_PerformsPrivilegedAction()
    {
        var events = new[] { TestEvents.Event(EventType.PrivilegeAction, TestEvents.Weekday, actor: "mallory") };

        var alert = Assert.Single(new PrivilegeEscalationDetector(Options).Detect(events).ToList());
        Assert.Equal("privilege_escalation", alert.DetectorName);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Contains("T1548", alert.MitreTechniques);
    }

    [Fact]
    public void DoesNotFire_For_AuthorizedActor()
    {
        var events = new[] { TestEvents.Event(EventType.PrivilegeAction, TestEvents.Weekday, actor: "admin") };

        Assert.Empty(new PrivilegeEscalationDetector(Options).Detect(events).ToList());
    }
}
