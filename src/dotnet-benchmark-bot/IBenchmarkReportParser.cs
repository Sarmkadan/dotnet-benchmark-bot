using System.Text.Json;

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
    public async Task<IReadOnlyList<BenchmarkResult>> ParseAsync(Stream reportStream, CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(reportStream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("Benchmarks", out var benchmarks)
            || benchmarks.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Not a BenchmarkDotNet full report: missing 'Benchmarks' array");
        }

        var results = new List<BenchmarkResult>();
        foreach (var benchmark in benchmarks.EnumerateArray())
        {
            // A failed benchmark has no statistics; there is nothing to compare.
            if (!benchmark.TryGetProperty("Statistics", out var statistics) || statistics.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fullName = benchmark.GetProperty("FullName").GetString()!;
            var mean = statistics.GetProperty("Mean").GetDouble();
            var standardDeviation = statistics.GetProperty("StandardDeviation").GetDouble();
            results.Add(new BenchmarkResult(fullName, mean, standardDeviation, ReadAllocatedBytes(benchmark)));
        }

        return results;
    }

    /// <summary>
    /// Reads <c>Memory.BytesAllocatedPerOperation</c> from a benchmark element, or <c>null</c> when absent.
    /// </summary>
    /// <param name="benchmark">A single element of the <c>Benchmarks</c> array.</param>
    /// <returns>Allocated bytes per operation, or <c>null</c> if the report does not contain it.</returns>
    private static long? ReadAllocatedBytes(JsonElement benchmark)
    {
        if (!benchmark.TryGetProperty("Memory", out var memory) || memory.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!memory.TryGetProperty("BytesAllocatedPerOperation", out var bytes) || bytes.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return bytes.GetInt64();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryAsync(string artifactsDirectory, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
