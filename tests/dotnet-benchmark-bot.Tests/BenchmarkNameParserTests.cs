namespace DotnetBenchmarkBot.Tests;

public class BenchmarkNameParserTests
{
    [Fact]
    public void Parse_WithParameters_SplitsAllParts()
    {
        var name = BenchmarkNameParser.Parse("MyApp.Benchmarks.ParserBenchmarks.Parse(size: 1024, mode: Fast)");

        Assert.Equal(new BenchmarkName("MyApp.Benchmarks.ParserBenchmarks", "Parse", "size: 1024, mode: Fast"), name);
    }

    [Fact]
    public void Parse_WithoutParentheses_HasNullParameters()
    {
        var name = BenchmarkNameParser.Parse("MyApp.ParserBenchmarks.Parse");

        Assert.Equal(new BenchmarkName("MyApp.ParserBenchmarks", "Parse", null), name);
    }

    [Fact]
    public void Parse_EmptyParentheses_HasNullParameters()
    {
        var name = BenchmarkNameParser.Parse("MyApp.ParserBenchmarks.Parse()");

        Assert.Equal(new BenchmarkName("MyApp.ParserBenchmarks", "Parse", null), name);
    }

    [Fact]
    public void Parse_TypeWithoutNamespace_IsAccepted()
    {
        var name = BenchmarkNameParser.Parse("ParserBenchmarks.Parse(size: 1)");

        Assert.Equal(new BenchmarkName("ParserBenchmarks", "Parse", "size: 1"), name);
    }

    [Fact]
    public void Parse_InvalidName_ThrowsFormatExceptionWithInput()
    {
        var exception = Assert.Throws<FormatException>(() => BenchmarkNameParser.Parse("NoDotHere(size: 1)"));

        Assert.Contains("NoDotHere(size: 1)", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NoDot")]
    [InlineData("NoDot(size: 1)")]
    [InlineData(".Parse")]
    [InlineData("MyApp.")]
    [InlineData("MyApp.Parse(size: 1")]
    [InlineData("MyApp.Parse(size: 1)x")]
    public void TryParse_InvalidInput_ReturnsFalseAndNullResult(string? fullName)
    {
        var parsed = BenchmarkNameParser.TryParse(fullName!, out var result);

        Assert.False(parsed);
        Assert.Null(result);
    }

    [Fact]
    public void ShortName_DropsNamespace()
    {
        Assert.Equal(
            "ParserBenchmarks.Parse(size: 1024)",
            BenchmarkNameParser.ShortName("MyApp.Benchmarks.ParserBenchmarks.Parse(size: 1024)"));
    }

    [Fact]
    public void ShortName_WithoutParameters_OmitsParentheses()
    {
        Assert.Equal("ParserBenchmarks.Parse", BenchmarkNameParser.ShortName("MyApp.ParserBenchmarks.Parse"));
        Assert.Equal("ParserBenchmarks.Parse", BenchmarkNameParser.ShortName("MyApp.ParserBenchmarks.Parse()"));
    }

    [Fact]
    public void ShortName_Unparseable_ReturnsInputUnchanged()
    {
        Assert.Equal("NoDot(size: 1", BenchmarkNameParser.ShortName("NoDot(size: 1"));
    }
}
