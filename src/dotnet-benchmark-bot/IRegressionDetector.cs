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
/// <remarks>
/// Classification per pair, in order:
/// <list type="number">
/// <item>The time delta is trusted only if <c>|candidate - baseline|</c> exceeds
/// <see cref="RegressionThresholds.NoiseSigma"/> times the larger of the two standard deviations.</item>
/// <item>An allocation increase above <see cref="RegressionThresholds.AllocationRegressionRatio"/> is
/// <see cref="RegressionVerdict.Regressed"/> regardless of the noise gate.</item>
/// <item>A trusted mean ratio above <see cref="RegressionThresholds.MeanRegressionRatio"/> (or infinite) is
/// <see cref="RegressionVerdict.Regressed"/>; a trusted drop below
/// <see cref="RegressionThresholds.MeanImprovementRatio"/> is <see cref="RegressionVerdict.Improved"/>.
/// A NaN mean ratio is never trusted.</item>
/// <item>Otherwise an allocation drop beyond <see cref="RegressionThresholds.AllocationRegressionRatio"/>
/// is <see cref="RegressionVerdict.Improved"/>, and everything else is <see cref="RegressionVerdict.Unchanged"/>.</item>
/// </list>
/// </remarks>
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
        // Allocations are deterministic enough to report without the time noise gate.
        var allocationVerdict = RegressionVerdict.Unchanged;
        if (thresholds.AllocationRegressionRatio is { } allocationLimit && allocationRatio is { } allocRatio)
        {
            if (allocRatio - 1.0 > allocationLimit)
            {
                return RegressionVerdict.Regressed;
            }

            if (1.0 - allocRatio > allocationLimit)
            {
                allocationVerdict = RegressionVerdict.Improved;
            }
        }

        var noise = thresholds.NoiseSigma * Math.Max(baseline.StdDevNanoseconds, candidate.StdDevNanoseconds);
        var delta = candidate.MeanNanoseconds - baseline.MeanNanoseconds;
        var trusted = Math.Abs(delta) > noise;

        var timeVerdict = RegressionVerdict.Unchanged;
        if (trusted && !double.IsNaN(meanRatio))
        {
            if (double.IsPositiveInfinity(meanRatio) || meanRatio - 1.0 > thresholds.MeanRegressionRatio)
            {
                timeVerdict = RegressionVerdict.Regressed;
            }
            else if (1.0 - meanRatio > thresholds.MeanImprovementRatio)
            {
                timeVerdict = RegressionVerdict.Improved;
            }
        }

        return timeVerdict is RegressionVerdict.Regressed or RegressionVerdict.Improved
            ? timeVerdict
            : allocationVerdict;
    }
}
