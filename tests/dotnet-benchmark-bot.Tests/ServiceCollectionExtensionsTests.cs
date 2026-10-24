using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotnetBenchmarkBot.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDotnetBenchmarkBot_ResolvesDefaultStages()
    {
        using var provider = new ServiceCollection()
            .AddDotnetBenchmarkBot()
            .BuildServiceProvider(validateScopes: true);

        Assert.IsType<JsonBenchmarkReportParser>(provider.GetRequiredService<IBenchmarkReportParser>());
        Assert.IsType<ThresholdRegressionDetector>(provider.GetRequiredService<IRegressionDetector>());
        Assert.IsType<MarkdownComparisonReportFormatter>(provider.GetRequiredService<IComparisonReportFormatter>());
        Assert.IsType<BenchmarkPipeline>(provider.GetRequiredService<BenchmarkPipeline>());
    }

    [Fact]
    public void AddDotnetBenchmarkBot_RegistersStagesAsSingletons()
    {
        using var provider = new ServiceCollection()
            .AddDotnetBenchmarkBot()
            .BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IRegressionDetector>(), provider.GetRequiredService<IRegressionDetector>());
        Assert.Same(provider.GetRequiredService<BenchmarkPipeline>(), provider.GetRequiredService<BenchmarkPipeline>());
    }

    [Fact]
    public void AddDotnetBenchmarkBot_WithoutToken_DoesNotRegisterCommenter()
    {
        using var provider = new ServiceCollection()
            .AddDotnetBenchmarkBot()
            .BuildServiceProvider();

        Assert.Null(provider.GetService<IPullRequestCommenter>());
    }

    [Fact]
    public void AddDotnetBenchmarkBot_WithToken_RegistersCommenter()
    {
        using var provider = new ServiceCollection()
            .AddDotnetBenchmarkBot(o => o.GitHubToken = "test-token")
            .BuildServiceProvider();

        Assert.IsType<GitHubPullRequestCommenter>(provider.GetRequiredService<IPullRequestCommenter>());
    }

    [Fact]
    public void AddDotnetBenchmarkBot_AppliesConfigureCallback()
    {
        using var provider = new ServiceCollection()
            .AddDotnetBenchmarkBot(o => o.BaselineDirectory = "custom/baseline")
            .BuildServiceProvider();

        Assert.Equal("custom/baseline", provider.GetRequiredService<IOptions<BenchmarkBotOptions>>().Value.BaselineDirectory);
    }

    [Fact]
    public void AddDotnetBenchmarkBot_InvalidOptions_ThrowOnResolve()
    {
        using var provider = new ServiceCollection()
            .AddDotnetBenchmarkBot(o => o.Report = new ReportFormatOptions { CollapseAfterRows = 0 })
            .BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<BenchmarkBotOptions>>().Value);
    }

    [Fact]
    public void AddDotnetBenchmarkBot_NullServices_Throws()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() => services!.AddDotnetBenchmarkBot());
    }
}
