namespace DotnetBenchmarkBot.Tests;

public class ThresholdRegressionDetectorPairingTests
{
    [Fact]
    public void Compare_CandidateOnly_ReportsAddedWithoutBaseline()
    {
        var detector = new ThresholdRegressionDetector();
        var candidate = new BenchmarkResult("Bench.New", 100, 1, 64);

        var result = detector.Compare([], [candidate]);

        var comparison = Assert.Single(result);
        Assert.Equal("Bench.New", comparison.FullName);
        Assert.Equal(RegressionVerdict.Added, comparison.Verdict);
        Assert.Null(comparison.Baseline);
        Assert.Same(candidate, comparison.Candidate);
        Assert.True(double.IsNaN(comparison.MeanRatio));
    }

    [Fact]
    public void Compare_BaselineOnly_ReportsRemovedWithoutCandidate()
    {
        var detector = new ThresholdRegressionDetector();
        var baseline = new BenchmarkResult("Bench.Old", 100, 1, 64);

        var result = detector.Compare([baseline], []);

        var comparison = Assert.Single(result);
        Assert.Equal("Bench.Old", comparison.FullName);
        Assert.Equal(RegressionVerdict.Removed, comparison.Verdict);
        Assert.Same(baseline, comparison.Baseline);
        Assert.Null(comparison.Candidate);
        Assert.True(double.IsNaN(comparison.MeanRatio));
    }

    [Fact]
    public void Compare_UnsortedInput_ReturnsOrdinallySortedByFullName()
    {
        var detector = new ThresholdRegressionDetector();

        // Mixed case and shared prefixes: ordinal order puts "A" < "B" < "a" < "ab".
        var names = new[] { "a.Bench", "B.Bench", "A.Bench", "ab.Bench", "A.Bench.Nested", "C.Bench" };
        var expectedNames = names.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        var baseline = names.Select(n => new BenchmarkResult(n, 100, 1, null)).ToArray();
        var candidate = names.Select(n => new BenchmarkResult(n, 100, 1, null)).ToArray();
        new Random(42).Shuffle(baseline);
        new Random(7).Shuffle(candidate);

        var result = detector.Compare(baseline, candidate);

        Assert.Equal(expectedNames, result.Select(c => c.FullName));
    }

    [Fact]
    public void Compare_BothListsEmpty_ReturnsEmpty()
    {
        var detector = new ThresholdRegressionDetector();

        var result = detector.Compare([], []);

        Assert.Empty(result);
    }

    [Fact]
    public void Compare_SameNamesInBothLists_ProducesOneComparisonPerName()
    {
        var detector = new ThresholdRegressionDetector();
        var baseline = new[]
        {
            new BenchmarkResult("Bench.A", 100, 1, null),
            new BenchmarkResult("Bench.B", 200, 2, null),
        };
        var candidate = new[]
        {
            new BenchmarkResult("Bench.B", 200, 2, null),
            new BenchmarkResult("Bench.A", 100, 1, null),
        };

        var result = detector.Compare(baseline, candidate);

        Assert.Equal(2, result.Count);
        Assert.Equal(["Bench.A", "Bench.B"], result.Select(c => c.FullName));
        Assert.All(result, c =>
        {
            Assert.NotNull(c.Baseline);
            Assert.NotNull(c.Candidate);
        });
    }

    [Fact]
    public void Compare_PartiallyOverlapping_CountEqualsUnionSize()
    {
        var detector = new ThresholdRegressionDetector();
        var baseline = new[]
        {
            new BenchmarkResult("Bench.Shared", 100, 1, null),
            new BenchmarkResult("Bench.OnlyBase", 100, 1, null),
        };
        var candidate = new[]
        {
            new BenchmarkResult("Bench.Shared", 100, 1, null),
            new BenchmarkResult("Bench.OnlyCand", 100, 1, null),
        };

        var result = detector.Compare(baseline, candidate);

        Assert.Equal(3, result.Count);
        Assert.Equal(["Bench.OnlyBase", "Bench.OnlyCand", "Bench.Shared"], result.Select(c => c.FullName));
    }

    [Fact]
    public void Compare_PairedComparison_ReferencesExactInputInstances()
    {
        var detector = new ThresholdRegressionDetector();
        var baseline = new BenchmarkResult("Bench.A", 100, 1, 64);
        var candidate = new BenchmarkResult("Bench.A", 150, 1, 64);

        var comparison = Assert.Single(detector.Compare([baseline], [candidate]));

        Assert.Same(baseline, comparison.Baseline);
        Assert.Same(candidate, comparison.Candidate);
    }

    [Fact]
    public void Compare_NullThresholds_MatchesDefaultThresholds()
    {
        var detector = new ThresholdRegressionDetector();
        var baseline = new[]
        {
            new BenchmarkResult("Bench.Slower", 100, 1, 100),
            new BenchmarkResult("Bench.Faster", 200, 1, 100),
            new BenchmarkResult("Bench.Same", 300, 1, 100),
        };
        var candidate = new[]
        {
            new BenchmarkResult("Bench.Slower", 150, 1, 100),
            new BenchmarkResult("Bench.Faster", 100, 1, 100),
            new BenchmarkResult("Bench.Same", 301, 1, 100),
        };

        var withNull = detector.Compare(baseline, candidate, null);
        var withDefaults = detector.Compare(baseline, candidate, new RegressionThresholds());

        Assert.Equal(withDefaults.Select(c => c.Verdict), withNull.Select(c => c.Verdict));
        Assert.Equal(withDefaults.Select(c => c.MeanRatio), withNull.Select(c => c.MeanRatio));
    }

    [Fact]
    public void Compare_ZeroBaselineMean_ReturnsPositiveInfinityRatio()
    {
        var detector = new ThresholdRegressionDetector();
        var baseline = new BenchmarkResult("Bench.Zero", 0, 0, null);
        var candidate = new BenchmarkResult("Bench.Zero", 50, 0, null);

        var result = detector.Compare([baseline], [candidate]);

        var comparison = Assert.Single(result);
        Assert.Equal(double.PositiveInfinity, comparison.MeanRatio);
    }
}
