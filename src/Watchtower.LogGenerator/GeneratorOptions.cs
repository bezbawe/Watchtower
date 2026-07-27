namespace Watchtower.LogGenerator;

// Конфиг генератора синтетики (§9, §10.3): период, интенсивность фона и параметры
// управляемых сценариев аномалий. Биндится из секции "Generator" (appsettings.json,
// переменные окружения, CLI-аргументы вида --Generator:PeriodHours=48).
public class GeneratorOptions
{
    public const string SectionName = "Generator";

    // Куда слать события (HTTP API приёма). Профиль http у Watchtower.Api — порт 5116.
    public string ApiBaseUrl { get; set; } = "http://localhost:5116";

    // Сид ГПСЧ — фиксирует поток для воспроизводимости демо/тестов.
    public int Seed { get; set; } = 1337;

    // Период (в часах), на который «размазываются» timestamp'ы событий: [now - PeriodHours, now].
    public int PeriodHours { get; set; } = 24;

    // Интенсивность нормального фона: событий в час (итог = PeriodHours * NormalEventsPerHour).
    public int NormalEventsPerHour { get; set; } = 40;

    // Размер батча при отправке в API.
    public int SendBatchSize { get; set; } = 200;

    // Управляемые сценарии аномалий (доля аномалий задаётся их количеством/интенсивностью).
    public BruteForceScenario BruteForce { get; set; } = new();
    public OffHoursScenario OffHours { get; set; } = new();
    public GeoAnomalyScenario GeoAnomaly { get; set; } = new();
}

// Brute-force: всплеск неудачных логинов с одного IP за короткое окно (→ детектор T1110).
public class BruteForceScenario
{
    public bool Enabled { get; set; } = true;
    public int Incidents { get; set; } = 2;       // сколько всплесков за период
    public int Attempts { get; set; } = 30;       // неудачных логинов в одном всплеске
    public int WithinMinutes { get; set; } = 3;   // за сколько минут укладывается всплеск
}

// Off-hours: привилегированные действия в нетипичное (ночное) время.
public class OffHoursScenario
{
    public bool Enabled { get; set; } = true;
    public int Incidents { get; set; } = 3;
}

// Geo-аномалия (impossible travel): успешные логины одного пользователя из гео-удалённых IP
// за короткий срок.
public class GeoAnomalyScenario
{
    public bool Enabled { get; set; } = true;
    public int Incidents { get; set; } = 2;
}
