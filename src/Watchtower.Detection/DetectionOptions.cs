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
