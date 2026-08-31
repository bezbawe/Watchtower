using Watchtower.Detection.Detectors;
using Watchtower.Entities.Enums;

namespace Watchtower.Detection.Tests;

public class AccountCompromiseChainDetectorTests
{
    private static readonly CorrelationOptions Options = new() { WindowMinutes = 20 };

    private static List<Entities.Events.LogEvent> Chain(
        int failToSuccessMinutes = 2, int successToDataMinutes = 3, string ip = "203.0.113.5", string actor = "alice")
    {
        var fail = TestEvents.Event(EventType.LoginFailed, TestEvents.Weekday, sourceIp: ip);
        var success = TestEvents.Event(EventType.LoginSuccess, TestEvents.Weekday.AddMinutes(failToSuccessMinutes), actor: actor, sourceIp: ip);
        var dataAccess = TestEvents.Event(EventType.DataAccess, TestEvents.Weekday.AddMinutes(failToSuccessMinutes + successToDataMinutes), actor: actor);
        return [fail, success, dataAccess];
    }

    [Fact]
    public void Fires_When_FailedLogin_ThenSuccess_ThenDataAccess_SameChain()
    {
        var alerts = new AccountCompromiseChainDetector(Options).Detect(Chain()).ToList();

        var alert = Assert.Single(alerts);
        Assert.Equal("account_compromise_chain", alert.DetectorName);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(["T1110", "T1005"], alert.MitreTechniques);
        Assert.Equal(3, alert.RelatedEventIds.Count);
        Assert.Contains("alice", alert.Explanation);
    }

    [Fact]
    public void DoesNotFire_When_NoDataAccessFollows()
    {
        var events = Chain().Take(2).ToList(); // только fail + success

        var alerts = new AccountCompromiseChainDetector(Options).Detect(events).ToList();

        Assert.Empty(alerts);
    }

    [Fact]
    public void DoesNotFire_When_SuccessFromDifferentIp()
    {
        var fail = TestEvents.Event(EventType.LoginFailed, TestEvents.Weekday, sourceIp: "203.0.113.5");
        var success = TestEvents.Event(EventType.LoginSuccess, TestEvents.Weekday.AddMinutes(2), actor: "alice", sourceIp: "198.51.100.9");
        var dataAccess = TestEvents.Event(EventType.DataAccess, TestEvents.Weekday.AddMinutes(5), actor: "alice");

        var alerts = new AccountCompromiseChainDetector(Options).Detect([fail, success, dataAccess]).ToList();

        Assert.Empty(alerts);
    }

    [Fact]
    public void DoesNotFire_When_DataAccessByDifferentActor()
    {
        var fail = TestEvents.Event(EventType.LoginFailed, TestEvents.Weekday, sourceIp: "203.0.113.5");
        var success = TestEvents.Event(EventType.LoginSuccess, TestEvents.Weekday.AddMinutes(2), actor: "alice", sourceIp: "203.0.113.5");
        var dataAccess = TestEvents.Event(EventType.DataAccess, TestEvents.Weekday.AddMinutes(5), actor: "bob");

        var alerts = new AccountCompromiseChainDetector(Options).Detect([fail, success, dataAccess]).ToList();

        Assert.Empty(alerts);
    }

    [Fact]
    public void DoesNotFire_When_FailToSuccessHopExceedsWindow()
    {
        var alerts = new AccountCompromiseChainDetector(Options)
            .Detect(Chain(failToSuccessMinutes: 25, successToDataMinutes: 3))
            .ToList();

        Assert.Empty(alerts);
    }

    [Fact]
    public void DoesNotFire_When_SuccessToDataAccessHopExceedsWindow()
    {
        var alerts = new AccountCompromiseChainDetector(Options)
            .Detect(Chain(failToSuccessMinutes: 2, successToDataMinutes: 25))
            .ToList();

        Assert.Empty(alerts);
    }
}
