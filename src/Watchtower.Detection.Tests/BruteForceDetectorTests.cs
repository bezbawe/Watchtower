using Watchtower.Detection.Detectors;
using Watchtower.Entities.Enums;

namespace Watchtower.Detection.Tests;

public class BruteForceDetectorTests
{
    private static readonly BruteForceOptions Options = new() { Threshold = 5, WindowMinutes = 15 };

    private static List<Entities.Events.LogEvent> Failures(int count, int stepMinutes, string ip = "77.240.1.10")
        => Enumerable.Range(0, count)
            .Select(i => TestEvents.Event(EventType.LoginFailed, TestEvents.Weekday.AddMinutes(i * stepMinutes), actor: "alice", sourceIp: ip))
            .ToList();

    [Fact]
    public void Fires_When_ThresholdFailures_FromOneIp_WithinWindow()
    {
        var alerts = new BruteForceDetector(Options).Detect(Failures(count: 6, stepMinutes: 1)).ToList();

        var alert = Assert.Single(alerts);
        Assert.Equal("brute_force", alert.DetectorName);
        Assert.Equal(AlertSeverity.High, alert.Severity);
        Assert.Contains("T1110", alert.MitreTechniques);
        Assert.Contains("77.240.1.10", alert.Explanation);
        Assert.True(alert.RelatedEventIds.Count >= Options.Threshold);
    }

    [Fact]
    public void DoesNotFire_When_FailuresBelowThreshold()
    {
        var alerts = new BruteForceDetector(Options).Detect(Failures(count: 4, stepMinutes: 1)).ToList();

        Assert.Empty(alerts);
    }

    [Fact]
    public void DoesNotFire_When_FailuresSpreadBeyondWindow()
    {
        // 6 неудач по одной каждые 5 минут: в любое 15-минутное окно попадает максимум 4.
        var alerts = new BruteForceDetector(Options).Detect(Failures(count: 6, stepMinutes: 5)).ToList();

        Assert.Empty(alerts);
    }
}
