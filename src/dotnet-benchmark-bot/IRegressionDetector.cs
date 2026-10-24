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
    {
        thresholds ??= new RegressionThresholds();

        var baselineByName = baseline.ToDictionary(r => r.FullName, StringComparer.Ordinal);
        var candidateByName = candidate.ToDictionary(r => r.FullName, StringComparer.Ordinal);

        var names = baselineByName.Keys.Union(candidateByName.Keys, StringComparer.Ordinal).ToList();
        names.Sort(StringComparer.Ordinal);

        var comparisons = new List<BenchmarkComparison>(names.Count);
        foreach (var name in names)
        {
            baselineByName.TryGetValue(name, out var baseResult);
            candidateByName.TryGetValue(name, out var candResult);

            if (baseResult is null)
            {
                comparisons.Add(new BenchmarkComparison(
                    name, null, candResult, double.NaN, null, RegressionVerdict.Added));
                continue;
            }

            if (candResult is null)
            {
                comparisons.Add(new BenchmarkComparison(
                    name, baseResult, null, double.NaN, null, RegressionVerdict.Removed));
                continue;
            }

            var meanRatio = baseResult.MeanNanoseconds == 0
                ? (candResult.MeanNanoseconds > 0 ? double.PositiveInfinity : 1.0)
                : candResult.MeanNanoseconds / baseResult.MeanNanoseconds;

            double? allocationRatio = baseResult.AllocatedBytes is { } baseBytes
                && candResult.AllocatedBytes is { } candBytes
                && baseBytes > 0
                    ? (double)candBytes / baseBytes
                    : null;

            var verdict = Classify(baseResult, candResult, meanRatio, allocationRatio, thresholds);
            comparisons.Add(new BenchmarkComparison(
                name, baseResult, candResult, meanRatio, allocationRatio, verdict));
        }

        return comparisons;
    }

    private static RegressionVerdict Classify(
        BenchmarkResult baseline,
        BenchmarkResult candidate,
        double meanRatio,
        double? allocationRatio,
        RegressionThresholds thresholds)
    {
        // TODO: sigma gate + ratio thresholds
        return RegressionVerdict.Unchanged;
    }
}
