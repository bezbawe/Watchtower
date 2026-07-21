using Watchtower.Ingestion.Parsing;

namespace Watchtower.Ingestion.Tests;

public class LogfmtParserTests
{
    private readonly LogfmtParser _parser = new();

    [Fact]
    public void ParseLine_FullLine_MapsKnownKeys_AndQuotedMessage()
    {
        var dto = _parser.ParseLine(
            "2026-08-30T12:00:00Z source=auth-service severity=warning type=login_failed " +
            "actor=alice ip=203.0.113.7 msg=\"Failed login for user alice\"");

        Assert.NotNull(dto);
        Assert.Equal(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero), dto!.Timestamp);
        Assert.Equal("auth-service", dto.Source);
        Assert.Equal("warning", dto.Severity);
        Assert.Equal("login_failed", dto.EventType);
        Assert.Equal("alice", dto.Actor);
        Assert.Equal("203.0.113.7", dto.SourceIp);
        Assert.Equal("Failed login for user alice", dto.Message);
    }

    [Fact]
    public void ParseLine_IsOrderIndependent()
    {
        var dto = _parser.ParseLine("severity=error ip=10.0.0.1 source=svc type=logout actor=bob");

        Assert.NotNull(dto);
        Assert.Equal("svc", dto!.Source);
        Assert.Equal("error", dto.Severity);
        Assert.Equal("logout", dto.EventType);
        Assert.Equal("bob", dto.Actor);
        Assert.Equal("10.0.0.1", dto.SourceIp);
    }

    [Fact]
    public void ParseLine_UnknownKeys_GoIntoFields()
    {
        var dto = _parser.ParseLine("source=svc attempt=3 method=password");

        Assert.NotNull(dto);
        Assert.NotNull(dto!.Fields);
        Assert.Equal("3", dto.Fields!["attempt"]);
        Assert.Equal("password", dto.Fields["method"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseLine_BlankLine_ReturnsNull(string line)
    {
        Assert.Null(_parser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_QuotedValue_SupportsEscapedQuote()
    {
        var dto = _parser.ParseLine("msg=\"say \\\"hi\\\" now\"");

        Assert.NotNull(dto);
        Assert.Equal("say \"hi\" now", dto!.Message);
    }

    [Fact]
    public void Parse_MultiLine_SkipsBlankLines_AndReturnsOnePerLine()
    {
        var text = "source=a type=login_success\n\n   \nsource=b type=logout\n";

        var events = _parser.Parse(text).ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal("a", events[0].Source);
        Assert.Equal("b", events[1].Source);
    }
}
