namespace Watchtower.Detection.MachineLearning;

// Результат L3-оценки последней точки временного ряда через ML.NET (SSA spike detection).
// Всё нужное для объяснимого алерта (почему сработал: числа модели), аналогично StatisticalAnomaly (L2).
public record MlSpikeAnomaly(
    double Observed,   // событий в оцениваемый час
    double RawScore,   // сырое отклонение (raw score) SSA-модели для этой точки
    double PValue,     // p-value алерта (меньше — увереннее)
    bool IsSpike);     // true = ML.NET пометил точку как всплеск (Alert == 1)
