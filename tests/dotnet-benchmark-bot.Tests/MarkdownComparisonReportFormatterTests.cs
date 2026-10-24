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

    [Fact]
    public void Format_StartsWithDefaultMarker()
    {
        var output = new MarkdownComparisonReportFormatter().Format([C("Bench.A", RegressionVerdict.Regressed, 1.5)]);

        Assert.StartsWith("<!-- dotnet-benchmark-bot -->\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesCustomMarkerAndTitle()
    {
        var options = new ReportFormatOptions { Marker = "<!-- custom -->", Title = "My title" };

        var output = new MarkdownComparisonReportFormatter().Format([C("Bench.A", RegressionVerdict.Improved, 0.5)], options);

        Assert.StartsWith("<!-- custom -->\n## My title\n\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("<!-- dotnet-benchmark-bot -->", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_HideUnchangedTrue_OmitsUnchangedRowsButSummaryCountsThem()
    {
        var comparisons = new[]
        {
            C("Bench.Same", RegressionVerdict.Unchanged),
            C("Bench.Slow", RegressionVerdict.Regressed, 1.5),
        };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons);

        Assert.Contains("1 unchanged, 0 added, 0 removed", output, StringComparison.Ordinal);
        Assert.DoesNotContain("`Bench.Same`", output, StringComparison.Ordinal);
        Assert.Contains("`Bench.Slow`", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_HideUnchangedFalse_IncludesUnchangedRows()
    {
        var comparisons = new[]
        {
            C("Bench.Same", RegressionVerdict.Unchanged),
            C("Bench.Slow", RegressionVerdict.Regressed, 1.5),
        };
        var options = new ReportFormatOptions { HideUnchanged = false };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.Contains("| `Bench.Same` |", output, StringComparison.Ordinal);
        Assert.Contains(":white_circle: Unchanged |\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_RegressedRow_ShowsBacktickedNameEmojiAndPositivePercentage()
    {
        var output = new MarkdownComparisonReportFormatter().Format([C("Bench.Parse", RegressionVerdict.Regressed, 1.5)]);

        var row = output.Split('\n').Single(line => line.Contains("`Bench.Parse`", StringComparison.Ordinal));
        Assert.Contains(":red_circle:", row, StringComparison.Ordinal);
        Assert.Contains("+50.0%", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_EscapesPipeInFullName_KeepingRowColumnCount()
    {
        var output = new MarkdownComparisonReportFormatter().Format([C("Bench.Parse|x", RegressionVerdict.Improved, 0.5)]);

        var row = output.Split('\n').Single(line => line.Contains("Bench.Parse", StringComparison.Ordinal));
        Assert.Contains("`Bench.Parse\\|x`", row, StringComparison.Ordinal);
        // Six columns mean seven unescaped pipes; an unescaped pipe in the name would add one more.
        Assert.Equal(7, row.Replace("\\|", string.Empty, StringComparison.Ordinal).Count(ch => ch == '|'));
    }

    [Fact]
    public void Format_EmptyInput_RendersNoChangesWithoutTable()
    {
        var output = new MarkdownComparisonReportFormatter().Format([]);

        Assert.Contains("_No measurable changes._", output, StringComparison.Ordinal);
        Assert.DoesNotContain("| Benchmark |", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_MoreThanCollapseLimit_WrapsNonRegressedRowsInDetails()
    {
        var comparisons = new[]
        {
            C("Bench.A", RegressionVerdict.Improved, 0.5),
            C("Bench.B", RegressionVerdict.Improved, 0.5),
            C("Bench.C", RegressionVerdict.Improved, 0.5),
            C("Bench.D", RegressionVerdict.Added),
            C("Bench.E", RegressionVerdict.Added),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.Contains("<details>", output, StringComparison.Ordinal);
        Assert.Contains("<summary>", output, StringComparison.Ordinal);
        Assert.Contains("</details>", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AtCollapseLimit_DoesNotWrapInDetails()
    {
        var comparisons = new[]
        {
            C("Bench.A", RegressionVerdict.Improved, 0.5),
            C("Bench.B", RegressionVerdict.Added),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.DoesNotContain("<details>", output, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary>", output, StringComparison.Ordinal);
        Assert.DoesNotContain("</details>", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Collapsed_KeepsRegressedRowsOutsideDetails()
    {
        var comparisons = new[]
        {
            C("Bench.Reg", RegressionVerdict.Regressed, 1.5),
            C("Bench.A", RegressionVerdict.Improved, 0.5),
            C("Bench.B", RegressionVerdict.Improved, 0.5),
            C("Bench.C", RegressionVerdict.Added),
            C("Bench.D", RegressionVerdict.Added),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 2 };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        var regressedIndex = output.IndexOf("`Bench.Reg`", StringComparison.Ordinal);
        var detailsIndex = output.IndexOf("<details>", StringComparison.Ordinal);
        Assert.True(regressedIndex >= 0, "Regressed row is missing from the output.");
        Assert.True(detailsIndex >= 0, "Details block is missing from the output.");
        Assert.True(regressedIndex < detailsIndex, "Regressed row must appear before the <details> block.");
    }

    [Fact]
    public void Format_TableHeaderLineIsExact()
    {
        var output = new MarkdownComparisonReportFormatter().Format([C("Bench.A", RegressionVerdict.Regressed, 1.5)]);

        var lines = output.Split('\n');
        Assert.Contains("| Benchmark | Baseline | Candidate | Ratio | Alloc | Verdict |", lines);
    }

    [Fact]
    public void Format_UsesOnlyLineFeedNewlines()
    {
        var comparisons = new[]
        {
            C("Bench.Reg", RegressionVerdict.Regressed, 1.5, baseAlloc: 1024, candAlloc: 2048),
            C("Bench.A", RegressionVerdict.Improved, 0.5),
            C("Bench.B", RegressionVerdict.Added),
            C("Bench.Same", RegressionVerdict.Unchanged),
        };
        var options = new ReportFormatOptions { CollapseAfterRows = 1, HideUnchanged = false, Title = "T" };

        var output = new MarkdownComparisonReportFormatter().Format(comparisons, options);

        Assert.DoesNotContain("\r", output, StringComparison.Ordinal);
        Assert.Contains("\n", output, StringComparison.Ordinal);
    }

    private static BenchmarkComparison C(
        string name,
        RegressionVerdict verdict,
        double ratio = 1.0,
        double baseNs = 1000,
        long? baseAlloc = null,
        long? candAlloc = null)
    {
        var baseline = new BenchmarkResult(name, baseNs, 1, baseAlloc);
        var candidate = new BenchmarkResult(name, baseNs * ratio, 1, candAlloc);
        return new BenchmarkComparison(name, baseline, candidate, ratio, null, verdict);
    }
}
