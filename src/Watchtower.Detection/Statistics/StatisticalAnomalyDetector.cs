namespace Watchtower.Detection.Statistics;

// L2 statistical: оценивает ПОСЛЕДНЮЮ точку часового ряда против baseline из предыдущих точек.
// Baseline = скользящее среднее + стандартное отклонение окна; сигнал = z-score; EWMA — как
// сглаженная опорная линия для объяснения. Чистая логика без БД (аналогично L1-детекторам).
public class StatisticalAnomalyDetector(StatisticalOptions options)
{
    private const double Epsilon = 1e-9;

    // series — счётчики событий по часам, от старых к новым. Возвращает аномалию по последней
    // точке или null (нормально / недостаточно baseline / нулевой разброс).
    public StatisticalAnomaly? Evaluate(IReadOnlyList<int> series)
    {
        // Нужно >= MinBaselinePoints для baseline + 1 оцениваемая точка.
        if (series.Count < options.MinBaselinePoints + 1)
            return null;

        var baseline = new double[series.Count - 1];
        for (var i = 0; i < baseline.Length; i++)
            baseline[i] = series[i];
        var observed = (double)series[^1];

        var mean = baseline.Average();
        var variance = baseline.Select(x => (x - mean) * (x - mean)).Average();
        var stdDev = Math.Sqrt(variance);

        // Плоский baseline: z не определён — не флагаем (см. §9: борьба с ложными срабатываниями).
        if (stdDev < Epsilon)
            return null;

        var zScore = (observed - mean) / stdDev;
        if (Math.Abs(zScore) < options.ZScoreThreshold)
            return null;

        return new StatisticalAnomaly(observed, mean, stdDev, Ewma(baseline), zScore, zScore > 0);
    }

    private double Ewma(IReadOnlyList<double> values)
    {
        var s = values[0];
        for (var i = 1; i < values.Count; i++)
            s = options.EwmaAlpha * values[i] + (1 - options.EwmaAlpha) * s;
        return s;
    }
}
