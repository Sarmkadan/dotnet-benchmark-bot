namespace DotnetBenchmarkBot;

/// <summary>
/// Parses BenchmarkDotNet exporter output into <see cref="BenchmarkResult"/> instances.
/// </summary>
public interface IBenchmarkReportParser
{
    /// <summary>
    /// Parses a BenchmarkDotNet report (JSON exporter format, <c>*-report-full.json</c>).
    /// </summary>
    /// <param name="reportStream">Stream positioned at the beginning of the JSON document.</param>
    /// <param name="cancellationToken">Token to cancel the parse.</param>
    /// <returns>All benchmark measurements found in the report.</returns>
    Task<IReadOnlyList<BenchmarkResult>> ParseAsync(Stream reportStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses every <c>*-report-full.json</c> file under a BenchmarkDotNet artifacts directory.
    /// </summary>
    /// <param name="artifactsDirectory">Path to the <c>BenchmarkDotNet.Artifacts/results</c> directory.</param>
    /// <param name="cancellationToken">Token to cancel the parse.</param>
    /// <returns>All benchmark measurements across all report files.</returns>
    Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryAsync(string artifactsDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IBenchmarkReportParser"/> for the BenchmarkDotNet JSON exporter.
/// </summary>
public sealed class JsonBenchmarkReportParser : IBenchmarkReportParser
{
    /// <inheritdoc />
    public Task<IReadOnlyList<BenchmarkResult>> ParseAsync(Stream reportStream, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryAsync(string artifactsDirectory, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
