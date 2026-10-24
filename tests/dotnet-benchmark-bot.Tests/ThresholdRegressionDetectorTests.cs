namespace DotnetBenchmarkBot.Tests;

public class ThresholdRegressionDetectorTests
{
    private static RegressionVerdict Classify(
        double baseMean, double baseStdDev, long? baseBytes,
        double candMean, double candStdDev, long? candBytes,
        RegressionThresholds? thresholds = null)
    {
        var baseline = new[] { new BenchmarkResult("Bench.A", baseMean, baseStdDev, baseBytes) };
        var candidate = new[] { new BenchmarkResult("Bench.A", candMean, candStdDev, candBytes) };
        return new ThresholdRegressionDetector().Compare(baseline, candidate, thresholds).Single().Verdict;
    }

    [Fact]
    public void Compare_TrustedSlowdown_IsRegressed()
    {
        var verdict = Classify(100, 1, null, 120, 1, null);

        Assert.Equal(RegressionVerdict.Regressed, verdict);
    }

    [Fact]
    public void Compare_SlowdownWithinNoise_IsUnchanged()
    {
        // Delta of 20 is below 2 * max(stddev) = 2 * 15 = 30, so the time delta is not trusted.
        var verdict = Classify(100, 15, null, 120, 1, null);

        Assert.Equal(RegressionVerdict.Unchanged, verdict);
    }

    [Fact]
    public void Compare_TrustedSpeedup_IsImproved()
    {
        var verdict = Classify(100, 1, null, 80, 1, null);

        Assert.Equal(RegressionVerdict.Improved, verdict);
    }

    [Fact]
    public void Compare_AllocationRegression_IsRegressedEvenWithinTimeNoise()
    {
        var verdict = Classify(100, 50, 1000, 101, 50, 1100);

        Assert.Equal(RegressionVerdict.Regressed, verdict);
    }

    [Fact]
    public void Compare_AllocationRegressionDisabled_IgnoresAllocations()
    {
        var thresholds = new RegressionThresholds { AllocationRegressionRatio = null };

        var verdict = Classify(100, 1, 1000, 100, 1, 5000, thresholds);

        Assert.Equal(RegressionVerdict.Unchanged, verdict);
    }

    [Fact]
    public void Compare_AllocationDropWithStableTime_IsImproved()
    {
        var verdict = Classify(100, 1, 1000, 100, 1, 800);

        Assert.Equal(RegressionVerdict.Improved, verdict);
    }

    [Fact]
    public void Compare_TrustedTimeRegressionWinsOverAllocationImprovement()
    {
        var verdict = Classify(100, 1, 1000, 120, 1, 800);

        Assert.Equal(RegressionVerdict.Regressed, verdict);
    }

    [Fact]
    public void Compare_ZeroBaselineMean_IsRegressedWhenTrusted()
    {
        var verdict = Classify(0, 0, null, 10, 0, null);

        Assert.Equal(RegressionVerdict.Regressed, verdict);
    }

    [Fact]
    public void Compare_NaNMean_IsUnchanged()
    {
        var verdict = Classify(100, 1, null, double.NaN, 1, null);

        Assert.Equal(RegressionVerdict.Unchanged, verdict);
    }
}
