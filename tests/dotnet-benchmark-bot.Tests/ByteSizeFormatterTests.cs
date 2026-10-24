namespace DotnetBenchmarkBot.Tests;

public class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5_242_880, "5 MB")]
    [InlineData(1_610_612_736, "1.5 GB")]
    [InlineData(1_048_575, "1 MB")]
    [InlineData(-512, "-512 B")]
    [InlineData(-1536, "-1.5 KB")]
    public void FormatBytes_PicksUnitAndTrimsZeros(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.FormatBytes(bytes));
    }

    [Fact]
    public void FormatBytes_Null_ReturnsDash()
    {
        Assert.Equal("-", ByteSizeFormatter.FormatBytes(null));
    }

    [Theory]
    [InlineData(512L, 1024L, "+512 B")]
    [InlineData(1024L, 512L, "-512 B")]
    [InlineData(1024L, 2560L, "+1.5 KB")]
    [InlineData(2048L, 2048L, "0 B")]
    [InlineData(0L, 5_242_880L, "+5 MB")]
    public void FormatDelta_SignsCandidateMinusBaseline(long baseline, long candidate, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.FormatDelta(baseline, candidate));
    }

    [Theory]
    [InlineData(null, 1024L)]
    [InlineData(1024L, null)]
    [InlineData(null, null)]
    public void FormatDelta_MissingSide_ReturnsDash(long? baseline, long? candidate)
    {
        Assert.Equal("-", ByteSizeFormatter.FormatDelta(baseline, candidate));
    }
}
