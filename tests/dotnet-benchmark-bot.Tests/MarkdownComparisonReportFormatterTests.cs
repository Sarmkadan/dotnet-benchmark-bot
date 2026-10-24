using DotnetBenchmarkBot;

namespace DotnetBenchmarkBot.Tests;

public class MarkdownComparisonReportFormatterTests
{
    private static readonly BenchmarkResult Base = new("Bench.Parse", 1000, 1, 1024);
    private static readonly BenchmarkResult Slow = new("Bench.Parse", 1500, 1, 2048);

    [Fact]
    public void Format_StartsWithMarkerAndSummary()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.Parse", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
        };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons);

        Assert.StartsWith("<!-- dotnet-benchmark-bot -->\n", output, StringComparison.Ordinal);
        Assert.Contains("**Benchmarks:** 1 total, 1 regressed :red_circle:, 0 improved, 0 unchanged, 0 added, 0 removed\n\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_RendersRegressedRow()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.Parse", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
        };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons);

        Assert.Contains("| `Bench.Parse` | 1 us | 1.5 us | +50.0% | +1 KB | :red_circle: Regressed |\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_HidesUnchangedByDefault_AndPrintsNoChangesWhenEmpty()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.Parse", Base, Base, 1.0, 1.0, RegressionVerdict.Unchanged),
        };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons);

        Assert.Contains("_No measurable changes._\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("| Benchmark |", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ShowsUnchangedWhenRequested()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.Parse", Base, Base, 1.0, 1.0, RegressionVerdict.Unchanged),
        };
        var options = new ReportFormatOptions { HideUnchanged = false };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.Contains(":white_circle: Unchanged |\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_EscapesPipesInBenchmarkName()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.Parse(a|b)", null, Slow, double.NaN, null, RegressionVerdict.Added),
        };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons);

        Assert.Contains("| `Bench.Parse(a\\|b)` | - | 1.5 us |", output, StringComparison.Ordinal);
        Assert.Contains(":new: Added |\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesTitleWhenProvided()
    {
        var options = new ReportFormatOptions { Title = "Perf report" };

        var output = new MarkdownComparisonReportFormatter().Format([], options);

        Assert.Contains("## Perf report\n\n", output, StringComparison.Ordinal);
    }
}
