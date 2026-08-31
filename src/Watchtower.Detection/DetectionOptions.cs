using Watchtower.Entities.Enums;

namespace Watchtower.Detection;

// Пороги правил L1 — вынесены в конфиг (секция "Detection") для обсуждения trade-off
// чувствительность ↔ число ложных срабатываний.
public class DetectionOptions
{
    public const string SectionName = "Detection";

    public BruteForceOptions BruteForce { get; set; } = new();
    public OffHoursOptions OffHours { get; set; } = new();
    public PrivilegeEscalationOptions PrivilegeEscalation { get; set; } = new();
    public ImpossibleTravelOptions ImpossibleTravel { get; set; } = new();
    public StatisticalOptions Statistical { get; set; } = new();
    public MlOptions Ml { get; set; } = new();
    public CorrelationOptions Correlation { get; set; } = new();
}

public class BruteForceOptions
{
    public int Threshold { get; set; } = 5;       // неудачных логинов с одного IP
    public int WindowMinutes { get; set; } = 15;  // за это окно
}

public class OffHoursOptions
{
    public int BusinessStartHour { get; set; } = 8;   // включительно (UTC)
    public int BusinessEndHour { get; set; } = 18;    // исключительно (UTC)

    // Какие события считаем «чувствительными» для нерабочего времени.
    public List<EventType> SensitiveEventTypes { get; set; } =
        [EventType.PrivilegeAction, EventType.ConfigChange];
}

public class PrivilegeEscalationOptions
{
    // Кто имеет право на привилегированные действия; остальные акторы -> эскалация.
    public List<string> AuthorizedActors { get; set; } = ["admin", "root", "svc_backup"];
}

public class ImpossibleTravelOptions
{
    public int WindowMinutes { get; set; } = 60;  // смена страны быстрее этого = невозможно
}

// Correlation: неудачный логин -> успешный логин с того же IP -> доступ к данным тем же актором.
public class CorrelationOptions
{
    public int WindowMinutes { get; set; } = 20;  // максимальный зазор между каждым звеном цепочки
}

// L2 — статистика по числу событий в час: baseline (скользящее среднее + EWMA) и z-score.
public class StatisticalOptions
{
    public int WindowHours { get; set; } = 72;        // сколько часов истории берём под baseline
    public int MinBaselinePoints { get; set; } = 12;  // минимум часовых точек, иначе baseline не строим
    public double ZScoreThreshold { get; set; } = 3.0; // |z| >= порога = аномалия
    public double EwmaAlpha { get; set; } = 0.3;      // сглаживание EWMA (0..1), для контекста в объяснении
}

// L3 — ML.NET SSA spike detection по числу событий в час: модель сама учит структуру ряда,
// без ручных порогов вроде z-score.
public class MlOptions
{
    public int WindowHours { get; set; } = 72;         // сколько часов истории берём под ряд
    public int MinBaselinePoints { get; set; } = 20;   // минимум часовых точек, иначе модель не обучаем
    public double Confidence { get; set; } = 95;       // доверительный интервал SSA-модели, %
    public int PValueHistoryLength { get; set; } = 8;  // окно для вычисления p-value
    public int SeasonalityWindowSize { get; set; } = 8; // верхняя граница длины сезонности
}
