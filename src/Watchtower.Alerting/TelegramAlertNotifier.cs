using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Watchtower.Entities.Alerts;

namespace Watchtower.Alerting;

// Шлёт алерт через Telegram Bot API (sendMessage). Токен/чат из TelegramOptions;
// если канал не настроен — тихо пропускаем (портфолио-демо работает и без Telegram).
public class TelegramAlertNotifier(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramAlertNotifier> logger) : ITelegramAlertNotifier
{
    private readonly TelegramOptions _options = options.Value;

    public async Task SendAsync(Alert alert, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            logger.LogDebug("Telegram channel is not configured; skipping alert {AlertId}", alert.Id);
            return;
        }

        var payload = new
        {
            chat_id = _options.ChatId,
            text = FormatMessage(alert),
            parse_mode = "HTML",
        };

        try
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Telegram sendMessage failed ({Status}): {Body}", (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram sendMessage threw for alert {AlertId}", alert.Id);
        }
    }

    // HTML-формат: severity + заголовок + объяснение (почему сработал) + техники MITRE.
    public static string FormatMessage(Alert alert)
    {
        var techniques = alert.MitreTechniques.Count > 0
            ? $"\nMITRE: {string.Join(", ", alert.MitreTechniques)}"
            : string.Empty;

        return
            $"\U0001F6A8 <b>[{alert.Severity}] {Escape(alert.Title)}</b>\n" +
            Escape(alert.Explanation) +
            techniques;
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
