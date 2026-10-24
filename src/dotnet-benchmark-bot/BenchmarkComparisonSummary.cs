namespace DotnetBenchmarkBot;

/// <summary>
/// Aggregate counts of benchmark verdicts for a single comparison run, used for PR comment headers and status lines.
/// </summary>
/// <param name="Total">Number of comparisons, regardless of verdict.</param>
/// <param name="Regressed">Number of comparisons classified as <see cref="RegressionVerdict.Regressed"/>.</param>
/// <param name="Improved">Number of comparisons classified as <see cref="RegressionVerdict.Improved"/>.</param>
/// <param name="Unchanged">Number of comparisons classified as <see cref="RegressionVerdict.Unchanged"/>.</param>
/// <param name="Added">Number of comparisons classified as <see cref="RegressionVerdict.Added"/>.</param>
/// <param name="Removed">Number of comparisons classified as <see cref="RegressionVerdict.Removed"/>.</param>
public sealed record BenchmarkComparisonSummary(int Total, int Regressed, int Improved, int Unchanged, int Added, int Removed)
{
    /// <summary>
    /// Gets a value indicating whether at least one benchmark regressed.
    /// </summary>
    public bool HasRegressions => Regressed > 0;

    /// <summary>
    /// Counts the verdicts of the given comparisons in a single pass.
    /// </summary>
    /// <param name="comparisons">Comparisons produced by the regression detector.</param>
    /// <returns>A summary whose <see cref="Total"/> equals the number of comparisons.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="comparisons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A comparison carries a verdict value that is not defined by <see cref="RegressionVerdict"/>.</exception>
    public static BenchmarkComparisonSummary FromComparisons(IReadOnlyList<BenchmarkComparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);

        var regressed = 0;
        var improved = 0;
        var unchanged = 0;
        var added = 0;
        var removed = 0;

        foreach (var comparison in comparisons)
        {
            switch (comparison.Verdict)
            {
                case RegressionVerdict.Regressed:
                    regressed++;
                    break;
                case RegressionVerdict.Improved:
                    improved++;
                    break;
                case RegressionVerdict.Unchanged:
                    unchanged++;
                    break;
                case RegressionVerdict.Added:
                    added++;
                    break;
                case RegressionVerdict.Removed:
                    removed++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparisons), comparison.Verdict, "Unknown regression verdict.");
            }
        }

        return new BenchmarkComparisonSummary(comparisons.Count, regressed, improved, unchanged, added, removed);
    }

    /// <summary>
    /// Renders the summary as a single GitHub-Markdown line. Emoji markers are shown only for non-zero
    /// regressed and improved counts.
    /// </summary>
    /// <returns>
    /// A line such as <c>**Benchmarks:** 12 total, 2 regressed :red_circle:, 3 improved :green_circle:, 6 unchanged, 1 added, 0 removed</c>.
    /// </returns>
    public string ToSummaryLine()
    {
        var regressedText = Regressed > 0
            ? FormattableString.Invariant($"{Regressed} regressed :red_circle:")
            : FormattableString.Invariant($"{Regressed} regressed");

        var improvedText = Improved > 0
            ? FormattableString.Invariant($"{Improved} improved :green_circle:")
            : FormattableString.Invariant($"{Improved} improved");

        return FormattableString.Invariant(
            $"**Benchmarks:** {Total} total, {regressedText}, {improvedText}, {Unchanged} unchanged, {Added} added, {Removed} removed");
    }

    /// <summary>
    /// Finds the regressed comparison with the highest <see cref="BenchmarkComparison.MeanRatio"/>.
    /// Comparisons with a NaN mean ratio are ignored.
    /// </summary>
    /// <param name="comparisons">Comparisons produced by the regression detector.</param>
    /// <returns>The worst regressed comparison, or <see langword="null"/> when no comparison is regressed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="comparisons"/> is <see langword="null"/>.</exception>
    public static BenchmarkComparison? WorstRegression(IReadOnlyList<BenchmarkComparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);

        BenchmarkComparison? worst = null;

        foreach (var comparison in comparisons)
        {
            if (comparison.Verdict != RegressionVerdict.Regressed || double.IsNaN(comparison.MeanRatio))
            {
                continue;
            }

            if (worst is null || comparison.MeanRatio > worst.MeanRatio)
            {
                worst = comparison;
            }
        }

        return worst;
    }
}
