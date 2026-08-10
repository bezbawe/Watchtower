using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;

namespace Watchtower.Alerting.Tests;

public class TelegramAlertNotifierTests
{
    [Fact]
    public void FormatMessage_IncludesSeverityTitleExplanationAndMitre()
    {
        var alert = new Alert
        {
            Severity = AlertSeverity.High,
            Title = "Brute-force: 6 failed logins from 203.0.113.9",
            Explanation = "6 failed logins within 2 min (threshold: 5 within 15 min).",
            MitreTechniques = ["T1110"],
        };

        var message = TelegramAlertNotifier.FormatMessage(alert);

        Assert.Contains("High", message);
        Assert.Contains("Brute-force", message);
        Assert.Contains("threshold: 5", message);
        Assert.Contains("T1110", message);
    }

    [Fact]
    public async Task SendAsync_WhenNotConfigured_DoesNotCallHttp()
    {
        var handler = new ThrowingHandler();
        using var http = new HttpClient(handler);
        var notifier = new TelegramAlertNotifier(
            http, Options.Create(new TelegramOptions()), NullLogger<TelegramAlertNotifier>.Instance);

        // Канал не настроен (пустые токен/чат) — должно быть no-op, без обращения к сети.
        await notifier.SendAsync(new Alert { Title = "x" }, CancellationToken.None);

        Assert.False(handler.Called);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            throw new InvalidOperationException("HTTP must not be called when Telegram is unconfigured");
        }
    }
}
