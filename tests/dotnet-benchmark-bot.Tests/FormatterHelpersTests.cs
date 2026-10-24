using System.Globalization;

namespace DotnetBenchmarkBot.Tests;

public class FormatterHelpersTests
{
    [Theory]
    [InlineData(250, "250 ns")]
    [InlineData(1234.5, "1.235 us")]
    [InlineData(45_600_000, "45.6 ms")]
    [InlineData(2_500_000_000, "2.5 s")]
    [InlineData(0, "0 ns")]
    public void DurationFormatter_FormatNanoseconds_PicksUnit(double nanoseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.FormatNanoseconds(nanoseconds));
    }

    [Fact]
    public void DurationFormatter_FormatNanoseconds_NaN_ReturnsNotAvailable()
    {
        Assert.Equal("n/a", DurationFormatter.FormatNanoseconds(double.NaN));
    }

    [Theory]
    [InlineData(1.125, "+12.5%")]
    [InlineData(0.9, "-10.0%")]
    [InlineData(1.0, "0.0%")]
    public void DurationFormatter_FormatRatio_ReturnsSignedPercent(double ratio, string expected)
    {
        Assert.Equal(expected, DurationFormatter.FormatRatio(ratio));
    }

    [Fact]
    public void DurationFormatter_FormatRatio_NaN_ReturnsNotAvailable()
    {
        Assert.Equal("n/a", DurationFormatter.FormatRatio(double.NaN));
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(5_242_880L, "5 MB")]
    public void ByteSizeFormatter_FormatBytes_FormatsOrDashesNull(long? bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.FormatBytes(bytes));
    }

    [Fact]
    public void ByteSizeFormatter_FormatDelta_Increase_StartsWithPlus()
    {
        Assert.StartsWith("+", ByteSizeFormatter.FormatDelta(1000, 1500), StringComparison.Ordinal);
    }

    [Fact]
    public void ByteSizeFormatter_FormatDelta_Decrease_StartsWithMinus()
    {
        Assert.StartsWith("-", ByteSizeFormatter.FormatDelta(1500, 1000), StringComparison.Ordinal);
    }

    [Fact]
    public void ByteSizeFormatter_FormatDelta_MissingBaseline_ReturnsDash()
    {
        Assert.Equal("-", ByteSizeFormatter.FormatDelta(null, 5));
    }

    [Fact]
    public void BenchmarkNameParser_Parse_SplitsTypeMethodAndParameters()
    {
        var name = BenchmarkNameParser.Parse("My.Ns.Benches.Parse(size: 1024, mode: Fast)");

        Assert.Equal("My.Ns.Benches", name.TypeName);
        Assert.Equal("Parse", name.MethodName);
        Assert.Equal("size: 1024, mode: Fast", name.Parameters);
    }

    [Fact]
    public void BenchmarkNameParser_Parse_WithoutParentheses_HasNullParameters()
    {
        var name = BenchmarkNameParser.Parse("A.B.Run");

        Assert.Equal("A.B", name.TypeName);
        Assert.Equal("Run", name.MethodName);
        Assert.Null(name.Parameters);
    }

    [Fact]
    public void BenchmarkNameParser_TryParse_NoDot_ReturnsFalse()
    {
        var parsed = BenchmarkNameParser.TryParse("NoDotHere", out var result);

        Assert.False(parsed);
        Assert.Null(result);
    }

    [Fact]
    public void BenchmarkNameParser_Parse_UnclosedParenthesis_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => BenchmarkNameParser.Parse("A.B.Run(x"));
    }

    [Fact]
    public void BenchmarkNameParser_ShortName_DropsNamespace()
    {
        Assert.Equal("Benches.Parse(size: 1)", BenchmarkNameParser.ShortName("My.Ns.Benches.Parse(size: 1)"));
    }

    [Fact]
    public void Formatters_UseInvariantCulture_EvenWhenCurrentCultureUsesComma()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var german = new CultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;

            // Guard: make sure the culture really would render a comma, otherwise the assertions below prove nothing.
            Assert.Equal("1,5", (1.5).ToString(german));

            Assert.Equal("1.235 us", DurationFormatter.FormatNanoseconds(1234.5));
            Assert.Equal("+12.5%", DurationFormatter.FormatRatio(1.125));
            Assert.Equal("1.5 KB", ByteSizeFormatter.FormatBytes(1536));
            Assert.Equal("+1.5 KB", ByteSizeFormatter.FormatDelta(1024, 2560));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
