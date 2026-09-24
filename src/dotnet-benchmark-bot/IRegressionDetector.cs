namespace DotnetBenchmarkBot;

/// <summary>
/// Thresholds that decide when a benchmark delta counts as a regression or improvement.
/// </summary>
public sealed class RegressionThresholds
{
    /// <summary>
    /// Relative mean-time increase above which a benchmark is <see cref="RegressionVerdict.Regressed"/>.
    /// Default 0.10 (10%).
    /// </summary>
    public double MeanRegressionRatio { get; init; } = 0.10;

    /// <summary>
    /// Relative mean-time decrease above which a benchmark is <see cref="RegressionVerdict.Improved"/>.
    /// Default 0.10 (10%).
    /// </summary>
    public double MeanImprovementRatio { get; init; } = 0.10;

    /// <summary>
    /// Relative allocation increase above which a benchmark is <see cref="RegressionVerdict.Regressed"/>.
    /// <see langword="null"/> disables allocation checks. Default 0.05 (5%).
    /// </summary>
    public double? AllocationRegressionRatio { get; init; } = 0.05;

    /// <summary>
    /// Number of standard deviations the candidate mean must move before a delta is trusted.
    /// Guards against flagging noisy benchmarks. Default 2.0.
    /// </summary>
    public double NoiseSigma { get; init; } = 2.0;
}

/// <summary>
/// Compares baseline and candidate benchmark runs and classifies each benchmark.
/// </summary>
public interface IRegressionDetector
{
    /// <summary>
    /// Matches benchmarks by <see cref="BenchmarkResult.FullName"/> and classifies each pair.
    /// </summary>
    /// <param name="baseline">Measurements from the base branch.</param>
    /// <param name="candidate">Measurements from the PR head.</param>
    /// <param name="thresholds">Thresholds to apply; <see langword="null"/> uses defaults.</param>
    /// <returns>One comparison per benchmark present in either run, ordered by <see cref="BenchmarkComparison.FullName"/>.</returns>
    IReadOnlyList<BenchmarkComparison> Compare(
        IReadOnlyList<BenchmarkResult> baseline,
        IReadOnlyList<BenchmarkResult> candidate,
        RegressionThresholds? thresholds = null);
}

/// <summary>
/// Default <see cref="IRegressionDetector"/> using ratio thresholds with a sigma-based noise gate.
/// </summary>
public sealed class ThresholdRegressionDetector : IRegressionDetector
{
    /// <inheritdoc />
    public IReadOnlyList<BenchmarkComparison> Compare(
        IReadOnlyList<BenchmarkResult> baseline,
        IReadOnlyList<BenchmarkResult> candidate,
        RegressionThresholds? thresholds = null)
        => throw new NotImplementedException();
}
