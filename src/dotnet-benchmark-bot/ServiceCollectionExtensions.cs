using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DotnetBenchmarkBot;

/// <summary>
/// Extension methods that register the benchmark bot services with a dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the benchmark bot pipeline, its default stage implementations and <see cref="BenchmarkBotOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="configure">Optional callback that customizes <see cref="BenchmarkBotOptions"/>. It may run more than once, so it should be free of side effects.</param>
    /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
    /// <remarks>
    /// The parser, detector and formatter are registered with <c>TryAdd</c>, so an earlier registration wins.
    /// <see cref="IPullRequestCommenter"/> is registered only when the configured <see cref="BenchmarkBotOptions.GitHubToken"/> is not empty.
    /// The options are validated when first resolved; an invalid configuration throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddDotnetBenchmarkBot(this IServiceCollection services, Action<BenchmarkBotOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<BenchmarkBotOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.Validate(o =>
        {
            o.Validate();
            return true;
        });

        services.TryAddSingleton<IBenchmarkReportParser, JsonBenchmarkReportParser>();
        services.TryAddSingleton<IRegressionDetector, ThresholdRegressionDetector>();
        services.TryAddSingleton<IComparisonReportFormatter, MarkdownComparisonReportFormatter>();
        services.TryAddSingleton<BenchmarkPipeline>();

        // The token must be known at registration time, so the callback is applied once here to a probe instance.
        var probe = new BenchmarkBotOptions();
        configure?.Invoke(probe);
        if (!string.IsNullOrWhiteSpace(probe.GitHubToken))
        {
            services.TryAddSingleton<IPullRequestCommenter>(sp =>
                new GitHubPullRequestCommenter(new HttpClient(), sp.GetRequiredService<IOptions<BenchmarkBotOptions>>().Value.GitHubToken!));
        }

        return services;
    }
}
