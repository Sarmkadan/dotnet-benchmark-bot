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
    public void Format_RendersSingleTableWhenRowsWithinCollapseLimit()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.A", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
            new BenchmarkComparison("Bench.B", Base, Slow, 1.5, 2.0, RegressionVerdict.Improved),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.DoesNotContain("<details>", output, StringComparison.Ordinal);
        Assert.DoesNotContain("### Regressions", output, StringComparison.Ordinal);
        Assert.EndsWith("|\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CollapsesNonRegressedRowsAndKeepsRegressionsVisible()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.A", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
            new BenchmarkComparison("Bench.B", Base, Slow, 1.5, 2.0, RegressionVerdict.Improved),
            new BenchmarkComparison("Bench.C", Base, Slow, 1.5, 2.0, RegressionVerdict.Added),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        var expected = "### Regressions\n\n"
            + "| Benchmark | Baseline | Candidate | Ratio | Alloc | Verdict |\n"
            + "| --- | ---: | ---: | ---: | ---: | --- |\n"
            + "| `Bench.A` | 1 us | 1.5 us | +50.0% | +1 KB | :red_circle: Regressed |\n"
            + "\n"
            + "<details>\n<summary>Show 2 other benchmarks</summary>\n\n"
            + "| Benchmark | Baseline | Candidate | Ratio | Alloc | Verdict |\n"
            + "| --- | ---: | ---: | ---: | ---: | --- |\n"
            + "| `Bench.B` | 1 us | 1.5 us | +50.0% | +1 KB | :green_circle: Improved |\n"
            + "| `Bench.C` | 1 us | 1.5 us | +50.0% | +1 KB | :new: Added |\n"
            + "\n</details>\n";

        Assert.EndsWith(expected, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CollapsesWholeTableWhenNoRegressions()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.A", Base, Slow, 1.5, 2.0, RegressionVerdict.Improved),
            new BenchmarkComparison("Bench.B", Base, Slow, 1.5, 2.0, RegressionVerdict.Added),
            new BenchmarkComparison("Bench.C", Base, Slow, 1.5, 2.0, RegressionVerdict.Added),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.Contains("<details>\n<summary>Show 3 benchmarks</summary>\n\n| Benchmark |", output, StringComparison.Ordinal);
        Assert.DoesNotContain("### Regressions", output, StringComparison.Ordinal);
        Assert.EndsWith("|\n\n</details>\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_RendersAllRegressedRowsWithoutDetailsBlock()
    {
        var comparisons = new[]
        {
            new BenchmarkComparison("Bench.A", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
            new BenchmarkComparison("Bench.B", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
            new BenchmarkComparison("Bench.C", Base, Slow, 1.5, 2.0, RegressionVerdict.Regressed),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.Contains("### Regressions\n\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("<details>", output, StringComparison.Ordinal);
        Assert.EndsWith("| :red_circle: Regressed |\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesTitleWhenProvided()
    {
        var options = new ReportFormatOptions { Title = "Perf report" };

        var output = new MarkdownComparisonReportFormatter().Format([], options);

        Assert.Contains("## Perf report\n\n", output, StringComparison.Ordinal);
    }
}
