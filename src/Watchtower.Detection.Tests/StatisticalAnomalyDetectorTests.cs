using Watchtower.Detection;
using Watchtower.Detection.Statistics;

namespace Watchtower.Detection.Tests;

public class StatisticalAnomalyDetectorTests
{
    private static StatisticalAnomalyDetector Detector(double zThreshold = 3.0) =>
        new(new StatisticalOptions { MinBaselinePoints = 12, ZScoreThreshold = zThreshold, EwmaAlpha = 0.3 });

    // Ровный (но не плоский) baseline вокруг ~40 + резкий всплеск последней точкой.
    private static readonly int[] BaselineAround40 =
        [38, 41, 39, 42, 40, 37, 43, 40, 39, 41, 38, 42, 40, 39];

    [Fact]
    public void Evaluate_SpikeInLastBucket_FlagsSpikeWithNumbers()
    {
        var series = BaselineAround40.Append(200).ToList();

        var anomaly = Detector().Evaluate(series);

        Assert.NotNull(anomaly);
        Assert.True(anomaly!.IsSpike);
        Assert.Equal(200, anomaly.Observed);
        Assert.True(anomaly.ZScore >= 3.0);
        Assert.InRange(anomaly.BaselineMean, 39, 41);
        Assert.True(anomaly.BaselineStdDev > 0);
    }

    [Fact]
    public void Evaluate_NormalLastBucket_ReturnsNull()
    {
        var series = BaselineAround40.Append(40).ToList();

        Assert.Null(Detector().Evaluate(series));
    }

    [Fact]
    public void Evaluate_DropToZero_FlagsAsNonSpike()
    {
        var series = BaselineAround40.Append(0).ToList();

        var anomaly = Detector().Evaluate(series);

        Assert.NotNull(anomaly);
        Assert.False(anomaly!.IsSpike);
        Assert.True(anomaly.ZScore <= -3.0);
    }

    [Fact]
    public void Evaluate_TooFewBaselinePoints_ReturnsNull()
    {
        // 11 baseline-точек + 1 наблюдаемая < MinBaselinePoints(12) + 1.
        var series = new[] { 40, 41, 39, 42, 38, 40, 41, 39, 42, 38, 40 }.Append(500).ToList();

        Assert.Null(Detector().Evaluate(series));
    }

    [Fact]
    public void Evaluate_FlatBaseline_ReturnsNull()
    {
        // Нулевой разброс — z не определён, не флагаем.
        var series = Enumerable.Repeat(40, 14).Append(200).ToList();

        Assert.Null(Detector().Evaluate(series));
    }
}
