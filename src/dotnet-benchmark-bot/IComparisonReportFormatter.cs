using System.Text;

namespace DotnetBenchmarkBot;

/// <summary>
/// Renders a set of <see cref="BenchmarkComparison"/> results into a human-readable report.
/// </summary>
public interface IComparisonReportFormatter
{
    /// <summary>
    /// Formats the comparisons as a report body suitable for the target medium.
    /// </summary>
    /// <param name="comparisons">Comparisons produced by <see cref="IRegressionDetector.Compare"/>.</param>
    /// <param name="options">Rendering options; <see langword="null"/> uses defaults.</param>
    /// <returns>The rendered report.</returns>
    string Format(IReadOnlyList<BenchmarkComparison> comparisons, ReportFormatOptions? options = null);
}

/// <summary>
/// Options controlling how a comparison report is rendered.
/// </summary>
public sealed class ReportFormatOptions
{
    /// <summary>Hide benchmarks whose verdict is <see cref="RegressionVerdict.Unchanged"/>. Default <see langword="true"/>.</summary>
    public bool HideUnchanged { get; init; } = true;

    /// <summary>Maximum number of rows before the table is collapsed into a <c>&lt;details&gt;</c> block. Default 25.</summary>
    public int CollapseAfterRows { get; init; } = 25;

    /// <summary>Optional title rendered as the first heading.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Hidden marker embedded in the output so an existing comment can be located and updated
    /// instead of posting a duplicate. Default <c>&lt;!-- dotnet-benchmark-bot --&gt;</c>.
    /// </summary>
    public string Marker { get; init; } = "<!-- dotnet-benchmark-bot -->";
}

/// <summary>
/// Formats comparisons as GitHub-flavored Markdown with a summary line and a results table.
/// </summary>
public sealed class MarkdownComparisonReportFormatter : IComparisonReportFormatter
{
    /// <inheritdoc />
    public string Format(IReadOnlyList<BenchmarkComparison> comparisons, ReportFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(comparisons);
        options ??= new ReportFormatOptions();

        var builder = new StringBuilder();

        builder.Append(options.Marker).Append('\n');

        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            builder.Append("## ").Append(options.Title).Append("\n\n");
        }

        builder.Append(BenchmarkComparisonSummary.FromComparisons(comparisons).ToSummaryLine()).Append("\n\n");

        var rows = comparisons
            .Where(c => !(options.HideUnchanged && c.Verdict == RegressionVerdict.Unchanged))
            .ToList();

        if (rows.Count == 0)
        {
            builder.Append("_No measurable changes._\n");
            return builder.ToString();
        }

        builder.Append("| Benchmark | Baseline | Candidate | Ratio | Alloc | Verdict |\n");
        builder.Append("| --- | ---: | ---: | ---: | ---: | --- |\n");

        foreach (var row in rows)
        {
            var name = "`" + row.FullName.Replace("|", "\\|", StringComparison.Ordinal) + "`";
            var baseline = row.Baseline is null ? "-" : DurationFormatter.FormatNanoseconds(row.Baseline.MeanNanoseconds);
            var candidate = row.Candidate is null ? "-" : DurationFormatter.FormatNanoseconds(row.Candidate.MeanNanoseconds);
            var ratio = DurationFormatter.FormatRatio(row.MeanRatio);
            var alloc = ByteSizeFormatter.FormatDelta(row.Baseline?.AllocatedBytes, row.Candidate?.AllocatedBytes);

            builder.Append("| ")
                .Append(name).Append(" | ")
                .Append(baseline).Append(" | ")
                .Append(candidate).Append(" | ")
                .Append(ratio).Append(" | ")
                .Append(alloc).Append(" | ")
                .Append(FormatVerdict(row.Verdict)).Append(" |\n");
        }

        return builder.ToString();
    }

    private static string FormatVerdict(RegressionVerdict verdict) => verdict switch
    {
        RegressionVerdict.Regressed => $":red_circle: {verdict}",
        RegressionVerdict.Improved => $":green_circle: {verdict}",
        RegressionVerdict.Added => $":new: {verdict}",
        RegressionVerdict.Removed => $":wastebasket: {verdict}",
        RegressionVerdict.Unchanged => $":white_circle: {verdict}",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unknown regression verdict."),
    };
}
