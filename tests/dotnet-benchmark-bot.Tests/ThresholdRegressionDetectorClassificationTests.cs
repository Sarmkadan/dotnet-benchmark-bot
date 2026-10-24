namespace DotnetBenchmarkBot.Tests;

public class ThresholdRegressionDetectorClassificationTests
{
    private static BenchmarkResult R(string name, double mean, double stdDev = 1.0, long? alloc = null)
        => new(name, mean, stdDev, alloc);

    private static BenchmarkComparison Compare(
        BenchmarkResult baseline, BenchmarkResult candidate, RegressionThresholds? thresholds = null)
        => new ThresholdRegressionDetector()
            .Compare(new[] { baseline }, new[] { candidate }, thresholds)
            .Single();

    [Theory]
    [InlineData(125, RegressionVerdict.Regressed)]
    [InlineData(75, RegressionVerdict.Improved)]
    [InlineData(103, RegressionVerdict.Unchanged)]
    public void Compare_TimeOnlyChange_ClassifiedByMeanRatio(double candidateMean, RegressionVerdict expected)
    {
        var comparison = Compare(R("Bench.A", 100), R("Bench.A", candidateMean));

        Assert.Equal(expected, comparison.Verdict);
    }

    [Fact]
    public void Compare_25PercentSlowerWithTinyStdDev_ReportsMeanRatio()
    {
        var comparison = Compare(R("Bench.A", 1000, 0.1), R("Bench.A", 1250, 0.1));

        Assert.Equal(RegressionVerdict.Regressed, comparison.Verdict);
        Assert.Equal(1.25, comparison.MeanRatio, 3);
    }

    [Fact]
    public void Compare_30PercentSlowerWithinNoiseGate_IsUnchanged()
    {
        // Delta 300 is below 2 * max(stddev) = 2 * 500 = 1000, so the time delta is not trusted.
        var comparison = Compare(R("Bench.A", 1000, 500), R("Bench.A", 1300, 500));

        Assert.Equal(RegressionVerdict.Unchanged, comparison.Verdict);
    }

    [Fact]
    public void Compare_30PercentSlowerWithLooseNoiseSigma_IsRegressed()
    {
        // Same input as above; NoiseSigma 0.1 makes the noise gate 100, so the delta of 300 is trusted.
        var thresholds = new RegressionThresholds { NoiseSigma = 0.1 };

        var comparison = Compare(R("Bench.A", 1000, 500), R("Bench.A", 1300, 500), thresholds);

        Assert.Equal(RegressionVerdict.Regressed, comparison.Verdict);
    }

    [Fact]
    public void Compare_AllocationGrowsTenPercentWithStableTime_IsRegressed()
    {
        var comparison = Compare(R("Bench.A", 100, 1, 1000), R("Bench.A", 100, 1, 1100));

        Assert.Equal(RegressionVerdict.Regressed, comparison.Verdict);
        Assert.Equal(1.1, comparison.AllocationRatio!.Value, 3);
    }

    [Fact]
    public void Compare_AllocationGrowsTenPercentWithAllocationCheckDisabled_IsUnchanged()
    {
        var thresholds = new RegressionThresholds { AllocationRegressionRatio = null };

        var comparison = Compare(R("Bench.A", 100, 1, 1000), R("Bench.A", 100, 1, 1100), thresholds);

        Assert.Equal(RegressionVerdict.Unchanged, comparison.Verdict);
    }

    [Fact]
    public void Compare_AllocationDropsTwentyPercentWithStableTime_IsImproved()
    {
        var comparison = Compare(R("Bench.A", 100, 1, 1000), R("Bench.A", 100, 1, 800));

        Assert.Equal(RegressionVerdict.Improved, comparison.Verdict);
    }

    [Theory]
    [InlineData(null, 1100L)]
    [InlineData(1000L, null)]
    public void Compare_AllocationMissingOnOneSide_LeavesAllocationRatioNullAndTimeDecides(
        long? baseBytes, long? candBytes)
    {
        var comparison = Compare(R("Bench.A", 100, 1, baseBytes), R("Bench.A", 125, 1, candBytes));

        Assert.Null(comparison.AllocationRatio);
        Assert.Equal(RegressionVerdict.Regressed, comparison.Verdict);
    }

    [Fact]
    public void Compare_AllocationMissingOnOneSideWithStableTime_IsUnchangedDespiteByteDelta()
    {
        var comparison = Compare(R("Bench.A", 100, 1, null), R("Bench.A", 100, 1, 5000));

        Assert.Null(comparison.AllocationRatio);
        Assert.Equal(RegressionVerdict.Unchanged, comparison.Verdict);
    }

    [Fact]
    public void Compare_CustomMeanRegressionRatio_RaisesBarForSlowdown()
    {
        // 30% slower is below the custom 50% limit; delta 30 is trusted since stddev is 1.
        var thresholds = new RegressionThresholds { MeanRegressionRatio = 0.5 };

        var comparison = Compare(R("Bench.A", 100), R("Bench.A", 130), thresholds);

        Assert.Equal(RegressionVerdict.Unchanged, comparison.Verdict);
    }

    [Fact]
    public void Compare_CustomMeanRegressionRatio_StillRegressesAboveLimit()
    {
        var thresholds = new RegressionThresholds { MeanRegressionRatio = 0.5 };

        var comparison = Compare(R("Bench.A", 100), R("Bench.A", 160), thresholds);

        Assert.Equal(RegressionVerdict.Regressed, comparison.Verdict);
    }
}
