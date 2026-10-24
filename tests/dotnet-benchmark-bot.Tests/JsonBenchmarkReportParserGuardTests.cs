namespace DotnetBenchmarkBot.Tests;

public class JsonBenchmarkReportParserGuardTests
{
    [Fact]
    public void ParseAsync_NullStream_ThrowsSynchronously()
    {
        var parser = new JsonBenchmarkReportParser();

        Assert.Throws<ArgumentNullException>(() => { _ = parser.ParseAsync(null!); });
    }

    [Fact]
    public void ParseDirectoryAsync_WhitespaceDirectory_ThrowsSynchronously()
    {
        var parser = new JsonBenchmarkReportParser();

        Assert.Throws<ArgumentException>(() => { _ = parser.ParseDirectoryAsync("  "); });
    }
}
