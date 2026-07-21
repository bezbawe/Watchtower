using Watchtower.Entities.Enums;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Enrichment;
using Watchtower.Ingestion.Normalization;

namespace Watchtower.Ingestion.Tests;

public class LogEventNormalizerTests
{
    private readonly LogEventNormalizer _normalizer = new(new StubGeoIpResolver());

    [Fact]
    public void Normalize_MissingTimestamp_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var ev = _normalizer.Normalize(new LogEventDto());
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(ev.Timestamp, before, after);
    }

    [Fact]
    public void Normalize_MissingSource_DefaultsToUnknown()
    {
        var ev = _normalizer.Normalize(new LogEventDto());
        Assert.Equal("unknown", ev.Source);
    }

    [Theory]
    [InlineData("login_failed", EventType.LoginFailed)]
    [InlineData("LoginSuccess", EventType.LoginSuccess)]
    [InlineData("privilege-action", EventType.PrivilegeAction)]
    [InlineData("nonsense", EventType.Unknown)]
    [InlineData(null, EventType.Unknown)]
    public void Normalize_ParsesEventType_Tolerantly(string? input, EventType expected)
    {
        var ev = _normalizer.Normalize(new LogEventDto { EventType = input });
        Assert.Equal(expected, ev.EventType);
    }

    [Theory]
    [InlineData("warning", Severity.Warning)]
    [InlineData("CRITICAL", Severity.Critical)]
    [InlineData("garbage", Severity.Info)]
    [InlineData(null, Severity.Info)]
    public void Normalize_ParsesSeverity_Tolerantly(string? input, Severity expected)
    {
        var ev = _normalizer.Normalize(new LogEventDto { Severity = input });
        Assert.Equal(expected, ev.Severity);
    }

    [Fact]
    public void Normalize_EnrichesGeo_ForKnownPublicIp()
    {
        var ev = _normalizer.Normalize(new LogEventDto { SourceIp = "203.0.113.7" });

        Assert.Equal("AU", ev.GeoCountry);
        Assert.Equal("Sydney", ev.GeoCity);
    }

    [Fact]
    public void Normalize_EnrichesGeo_ForPrivateIp_AsInternal()
    {
        var ev = _normalizer.Normalize(new LogEventDto { SourceIp = "192.168.1.10" });

        Assert.Equal("Internal", ev.GeoCountry);
        Assert.Equal("LAN", ev.GeoCity);
    }

    [Fact]
    public void Normalize_LeavesGeoNull_WhenNoSourceIp()
    {
        var ev = _normalizer.Normalize(new LogEventDto());

        Assert.Null(ev.GeoCountry);
        Assert.Null(ev.GeoCity);
    }

    [Fact]
    public void Normalize_CopiesFields()
    {
        var ev = _normalizer.Normalize(new LogEventDto
        {
            Fields = new Dictionary<string, string> { ["attempt"] = "3" }
        });

        Assert.Equal("3", ev.Fields["attempt"]);
    }
}
