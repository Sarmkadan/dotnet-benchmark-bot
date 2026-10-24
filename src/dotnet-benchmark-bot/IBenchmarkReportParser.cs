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
    /// <exception cref="ArgumentNullException"><paramref name="reportStream"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reportStream"/> is not readable.</exception>
    Task<IReadOnlyList<BenchmarkResult>> ParseAsync(Stream reportStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses every <c>*-report-full.json</c> file under a BenchmarkDotNet artifacts directory.
    /// </summary>
    /// <param name="artifactsDirectory">Path to the <c>BenchmarkDotNet.Artifacts/results</c> directory.</param>
    /// <param name="cancellationToken">Token to cancel the parse.</param>
    /// <returns>All benchmark measurements across all report files.</returns>
    /// <exception cref="ArgumentException"><paramref name="artifactsDirectory"/> is <c>null</c>, empty, or whitespace.</exception>
    Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryAsync(string artifactsDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IBenchmarkReportParser"/> for the BenchmarkDotNet JSON exporter.
/// </summary>
public sealed class JsonBenchmarkReportParser : IBenchmarkReportParser
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="reportStream"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reportStream"/> is not readable.</exception>
    public Task<IReadOnlyList<BenchmarkResult>> ParseAsync(Stream reportStream, CancellationToken cancellationToken = default)
    {
        // Validate synchronously so bad arguments throw at the call site, not inside the returned Task.
        ArgumentNullException.ThrowIfNull(reportStream);
        if (!reportStream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(reportStream));
        }

        return ParseCoreAsync(reportStream, cancellationToken);
    }

    /// <summary>
    /// Parses a validated report stream. See <see cref="ParseAsync"/>.
    /// </summary>
    private async Task<IReadOnlyList<BenchmarkResult>> ParseCoreAsync(Stream reportStream, CancellationToken cancellationToken)
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
    /// <exception cref="ArgumentException"><paramref name="artifactsDirectory"/> is <c>null</c>, empty, or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">The artifacts directory does not exist.</exception>
    public Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryAsync(string artifactsDirectory, CancellationToken cancellationToken = default)
    {
        // Validate synchronously so bad arguments throw at the call site, not inside the returned Task.
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsDirectory);

        return ParseDirectoryCoreAsync(artifactsDirectory, cancellationToken);
    }

    /// <summary>
    /// Parses a validated artifacts directory. See <see cref="ParseDirectoryAsync"/>.
    /// </summary>
    private async Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryCoreAsync(string artifactsDirectory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(artifactsDirectory))
        {
            throw new DirectoryNotFoundException($"Benchmark artifacts directory not found: '{artifactsDirectory}'");
        }

        var reportFiles = Directory.EnumerateFiles(artifactsDirectory, "*-report-full.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();

        var results = new List<BenchmarkResult>();
        var seenFullNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reportFile in reportFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = new FileStream(reportFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            foreach (var result in await ParseCoreAsync(stream, cancellationToken))
            {
                // The first occurrence wins; later duplicates of the same benchmark are ignored.
                if (seenFullNames.Add(result.FullName))
                {
                    results.Add(result);
                }
            }
        }

        return results;
    }
}
