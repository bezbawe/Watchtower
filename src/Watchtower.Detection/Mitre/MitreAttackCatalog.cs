namespace Watchtower.Detection.Mitre;

// Человекочитаемые названия техник MITRE ATT&CK, используемых детекторами (Alert.MitreTechniques
// хранит только id вроде "T1110"). Отображение — в алерте и на дашборде (§7).
public static class MitreAttackCatalog
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["T1110"] = "Brute Force",
        ["T1078"] = "Valid Accounts",
        ["T1548"] = "Abuse Elevation Control Mechanism",
        ["T1005"] = "Data from Local System",
    };

    // "T1110 — Brute Force"; неизвестный id возвращается как есть (детектор мог указать
    // технику, не занесённую сюда, — не считаем это ошибкой).
    public static string Describe(string techniqueId) =>
        Names.TryGetValue(techniqueId, out var name) ? $"{techniqueId} — {name}" : techniqueId;
}
