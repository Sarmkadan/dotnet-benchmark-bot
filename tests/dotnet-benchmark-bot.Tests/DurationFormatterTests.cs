namespace DotnetBenchmarkBot.Tests;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(250, "250 ns")]
    [InlineData(1234.5, "1.235 us")]
    [InlineData(45_600_000, "45.6 ms")]
    [InlineData(0, "0 ns")]
    [InlineData(5.5, "5.5 ns")]
    [InlineData(12.345, "12.35 ns")]
    [InlineData(999.9996, "1 us")]
    [InlineData(2_500_000_000, "2.5 s")]
    [InlineData(-1500, "-1.5 us")]
    public void FormatNanoseconds_PicksUnitAndPrecision(double nanoseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.FormatNanoseconds(nanoseconds));
    }

    [Fact]
    public void FormatNanoseconds_NonFinite_ReturnsWords()
    {
        Assert.Equal("n/a", DurationFormatter.FormatNanoseconds(double.NaN));
        Assert.Equal("inf", DurationFormatter.FormatNanoseconds(double.PositiveInfinity));
        Assert.Equal("-inf", DurationFormatter.FormatNanoseconds(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(1.125, "+12.5%")]
    [InlineData(0.9, "-10.0%")]
    [InlineData(1.0, "0.0%")]
    [InlineData(0.9999, "0.0%")]
    [InlineData(2.0, "+100.0%")]
    public void FormatRatio_ReturnsSignedPercentage(double ratio, string expected)
    {
        Assert.Equal(expected, DurationFormatter.FormatRatio(ratio));
    }

    [Fact]
    public void FormatRatio_NonFinite_ReturnsWords()
    {
        Assert.Equal("n/a", DurationFormatter.FormatRatio(double.NaN));
        Assert.Equal("+inf", DurationFormatter.FormatRatio(double.PositiveInfinity));
    }
}
