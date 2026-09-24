namespace DotnetBenchmarkBot;

/// <summary>
/// A single benchmark measurement as parsed from a BenchmarkDotNet report.
/// </summary>
/// <param name="FullName">Fully qualified benchmark name, e.g. <c>MyBenchmarks.Parse(size: 1024)</c>.</param>
/// <param name="MeanNanoseconds">Mean execution time in nanoseconds.</param>
/// <param name="StdDevNanoseconds">Standard deviation in nanoseconds.</param>
/// <param name="AllocatedBytes">Bytes allocated per operation, or <see langword="null"/> when the memory diagnoser was not enabled.</param>
public sealed record BenchmarkResult(
    string FullName,
    double MeanNanoseconds,
    double StdDevNanoseconds,
    long? AllocatedBytes);

/// <summary>
/// The outcome of comparing one benchmark between a baseline and a candidate run.
/// </summary>
/// <param name="FullName">Fully qualified benchmark name.</param>
/// <param name="Baseline">Baseline measurement, or <see langword="null"/> if the benchmark is new.</param>
/// <param name="Candidate">Candidate measurement, or <see langword="null"/> if the benchmark was removed.</param>
/// <param name="MeanRatio">Candidate mean divided by baseline mean. Values above 1.0 indicate a slowdown.</param>
/// <param name="AllocationRatio">Candidate allocations divided by baseline allocations, or <see langword="null"/> when unavailable.</param>
/// <param name="Verdict">Classification of the change.</param>
public sealed record BenchmarkComparison(
    string FullName,
    BenchmarkResult? Baseline,
    BenchmarkResult? Candidate,
    double MeanRatio,
    double? AllocationRatio,
    RegressionVerdict Verdict);

/// <summary>
/// Classification of a benchmark delta relative to configured thresholds.
/// </summary>
public enum RegressionVerdict
{
    /// <summary>Change is within the noise threshold.</summary>
    Unchanged,

    /// <summary>Candidate is measurably faster or allocates less.</summary>
    Improved,

    /// <summary>Candidate is slower or allocates more beyond the threshold.</summary>
    Regressed,

    /// <summary>Benchmark exists only in the candidate run.</summary>
    Added,

    /// <summary>Benchmark exists only in the baseline run.</summary>
    Removed,
}
