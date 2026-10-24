namespace DotnetBenchmarkBot.Tests;

public class BenchmarkBotOptionsTests
{
    [Fact]
    public void Defaults_Validate()
    {
        var options = new BenchmarkBotOptions();

        options.Validate();
    }

    [Fact]
    public void CollapseAfterRows_Zero_Throws()
    {
        var options = new BenchmarkBotOptions
        {
            Report = new ReportFormatOptions { CollapseAfterRows = 0 },
        };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("CollapseAfterRows", ex.Message);
    }

    [Fact]
    public void PullRequest_Malformed_Throws()
    {
        var options = new BenchmarkBotOptions { PullRequest = "bad" };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("PullRequest", ex.Message);
    }
}
