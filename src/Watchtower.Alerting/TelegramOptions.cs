namespace Watchtower.Alerting;

// Настройки Telegram-канала алертинга (секция "Telegram"). Пусто = канал отключён (no-op).
public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}
