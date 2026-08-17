namespace Watchtower.Detection.Statistics;

// Результат L2-оценки последней точки ряда: наблюдаемое значение против baseline.
// Всё нужное для объяснимого алерта (почему сработал: числа + пороги).
public record StatisticalAnomaly(
    double Observed,        // событий в оцениваемый час
    double BaselineMean,    // среднее по baseline-окну (скользящее среднее)
    double BaselineStdDev,  // разброс baseline
    double Ewma,            // экспоненциально сглаженный baseline (контекст тренда)
    double ZScore,          // (Observed - BaselineMean) / BaselineStdDev
    bool IsSpike);          // true = всплеск (z>0), false = провал (z<0)
