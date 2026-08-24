using Microsoft.ML;
using Microsoft.ML.Data;

namespace Watchtower.Detection.MachineLearning;

// L3 ML: SSA-based spike detection (ML.NET `DetectSpikeBySsa`) поверх часового ряда — без ручных
// правил/порогов вроде z-score (L2), модель сама учит структуру ряда и флагает точки, которые ей не
// соответствуют. Обучается на всей серии и оценивает ПОСЛЕДНЮЮ точку (семантика — как у L2).
// Чистая логика без БД (аналогично L1/L2).
public class SsaSpikeDetector(MlOptions options)
{
    // series — счётчики событий по часам, от старых к новым. Возвращает аномалию по последней
    // точке или null (нормально / недостаточно данных для обучения модели).
    public MlSpikeAnomaly? Evaluate(IReadOnlyList<int> series)
    {
        if (series.Count < options.MinBaselinePoints + 1)
            return null;

        var seasonalityWindowSize = Math.Min(options.SeasonalityWindowSize, series.Count / 2 - 1);
        var pvalueHistoryLength = Math.Min(options.PValueHistoryLength, series.Count / 2);

        var mlContext = new MLContext(seed: 0);
        var dataView = mlContext.Data.LoadFromEnumerable(series.Select(v => new SeriesPoint { Value = v }));

        var pipeline = mlContext.Transforms.DetectSpikeBySsa(
            outputColumnName: nameof(SpikePrediction.Prediction),
            inputColumnName: nameof(SeriesPoint.Value),
            confidence: options.Confidence,
            pvalueHistoryLength: pvalueHistoryLength,
            trainingWindowSize: series.Count,
            seasonalityWindowSize: seasonalityWindowSize);

        var transformed = pipeline.Fit(dataView).Transform(dataView);
        var predictions = mlContext.Data
            .CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false)
            .ToList();

        var last = predictions[^1].Prediction;
        var isSpike = last[0] == 1;
        if (!isSpike)
            return null;

        return new MlSpikeAnomaly(series[^1], last[1], last[2], true);
    }

    private class SeriesPoint
    {
        public float Value { get; set; }
    }

    private class SpikePrediction
    {
        [VectorType(3)]
        public double[] Prediction { get; set; } = null!;
    }
}
