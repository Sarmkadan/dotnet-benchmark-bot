namespace DotnetBenchmarkBot.Tests;

public class ThresholdRegressionDetectorGuardTests
{
    private static readonly BenchmarkResult[] SingleResult = [new("Bench.A", 100, 1, null)];

    [Fact]
    public void Compare_NullBaseline_ThrowsArgumentNullException()
    {
        var detector = new ThresholdRegressionDetector();

        Assert.Throws<ArgumentNullException>(() => detector.Compare(null!, SingleResult));
    }

    [Fact]
    public void Compare_NullCandidate_ThrowsArgumentNullException()
    {
        var detector = new ThresholdRegressionDetector();

        Assert.Throws<ArgumentNullException>(() => detector.Compare(SingleResult, null!));
    }

    [Fact]
    public void Compare_NegativeNoiseSigma_ThrowsArgumentOutOfRangeException()
    {
        var detector = new ThresholdRegressionDetector();
        var thresholds = new RegressionThresholds { NoiseSigma = -1.0 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => detector.Compare(SingleResult, SingleResult, thresholds));

        Assert.Equal("NoiseSigma", ex.ParamName);
    }

    [Theory]
    [InlineData("baseline")]
    [InlineData("candidate")]
    public void Compare_EmptyFullName_ThrowsArgumentExceptionNamingList(string listName)
    {
        var detector = new ThresholdRegressionDetector();
        var withEmptyName = new[] { new BenchmarkResult(string.Empty, 100, 1, null) };

        var ex = listName == "baseline"
            ? Assert.Throws<ArgumentException>(() => detector.Compare(withEmptyName, SingleResult))
            : Assert.Throws<ArgumentException>(() => detector.Compare(SingleResult, withEmptyName));

        Assert.Equal(listName, ex.ParamName);
        Assert.Contains(listName, ex.Message);
    }
}
