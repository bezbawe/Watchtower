using Watchtower.Detection;
using Watchtower.Detection.MachineLearning;

namespace Watchtower.Detection.Tests;

public class SsaSpikeDetectorTests
{
    private static SsaSpikeDetector Detector() =>
        new(new MlOptions { MinBaselinePoints = 20, Confidence = 95, PValueHistoryLength = 8, SeasonalityWindowSize = 8 });

    // Ровный (но не плоский) фон вокруг ~40 в час, без сезонности.
    private static readonly int[] SteadyBaseline =
        [38, 41, 39, 42, 40, 37, 43, 40, 39, 41, 38, 42, 40, 39, 41, 40, 38, 42, 39, 40];

    [Fact]
    public void Evaluate_SpikeInLastBucket_FlagsSpikeWithNumbers()
    {
        var series = SteadyBaseline.Append(300).ToList();

        var anomaly = Detector().Evaluate(series);

        Assert.NotNull(anomaly);
        Assert.True(anomaly!.IsSpike);
        Assert.Equal(300, anomaly.Observed);
    }

    [Fact]
    public void Evaluate_NormalLastBucket_ReturnsNull()
    {
        var series = SteadyBaseline.Append(40).ToList();

        Assert.Null(Detector().Evaluate(series));
    }

    [Fact]
    public void Evaluate_TooFewBaselinePoints_ReturnsNull()
    {
        // 19 точек + 1 наблюдаемая < MinBaselinePoints(20) + 1.
        var series = SteadyBaseline.Take(19).Append(500).ToList();

        Assert.Null(Detector().Evaluate(series));
    }
}
