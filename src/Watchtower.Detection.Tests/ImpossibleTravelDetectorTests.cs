using Watchtower.Detection.Detectors;
using Watchtower.Entities.Enums;

namespace Watchtower.Detection.Tests;

public class ImpossibleTravelDetectorTests
{
    private static readonly ImpossibleTravelOptions Options = new();

    [Fact]
    public void Fires_When_SameUser_DifferentCountries_ShortInterval()
    {
        var t0 = TestEvents.Weekday;
        var events = new[]
        {
            TestEvents.Event(EventType.LoginSuccess, t0, actor: "alice", sourceIp: "8.8.8.8", geoCountry: "US", geoCity: "Mountain View"),
            TestEvents.Event(EventType.LoginSuccess, t0.AddMinutes(10), actor: "alice", sourceIp: "123.10.20.30", geoCountry: "CN", geoCity: "Beijing"),
        };

        var alert = Assert.Single(new ImpossibleTravelDetector(Options).Detect(events).ToList());
        Assert.Equal("impossible_travel", alert.DetectorName);
        Assert.Equal(AlertSeverity.High, alert.Severity);
        Assert.Equal(2, alert.RelatedEventIds.Count);
    }

    [Fact]
    public void DoesNotFire_When_SameCountry()
    {
        var t0 = TestEvents.Weekday;
        var events = new[]
        {
            TestEvents.Event(EventType.LoginSuccess, t0, actor: "alice", geoCountry: "US", geoCity: "Mountain View"),
            TestEvents.Event(EventType.LoginSuccess, t0.AddMinutes(10), actor: "alice", geoCountry: "US", geoCity: "New York"),
        };

        Assert.Empty(new ImpossibleTravelDetector(Options).Detect(events).ToList());
    }

    [Fact]
    public void DoesNotFire_When_IntervalExceedsWindow()
    {
        var t0 = TestEvents.Weekday;
        var events = new[]
        {
            TestEvents.Event(EventType.LoginSuccess, t0, actor: "alice", geoCountry: "US"),
            TestEvents.Event(EventType.LoginSuccess, t0.AddHours(5), actor: "alice", geoCountry: "CN"),
        };

        Assert.Empty(new ImpossibleTravelDetector(Options).Detect(events).ToList());
    }

    [Fact]
    public void DoesNotFire_For_InternalGeo()
    {
        var t0 = TestEvents.Weekday;
        var events = new[]
        {
            TestEvents.Event(EventType.LoginSuccess, t0, actor: "alice", geoCountry: "Internal", geoCity: "LAN"),
            TestEvents.Event(EventType.LoginSuccess, t0.AddMinutes(10), actor: "alice", geoCountry: "Internal", geoCity: "LAN"),
        };

        Assert.Empty(new ImpossibleTravelDetector(Options).Detect(events).ToList());
    }
}
