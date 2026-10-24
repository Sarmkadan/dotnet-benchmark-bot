using System.Globalization;

namespace DotnetBenchmarkBot;

/// <summary>
/// Options that control when a set of benchmark comparisons fails the regression gate.
/// </summary>
public sealed class RegressionGateOptions
{
    /// <summary>
    /// Gets a value indicating whether regressions fail the gate. When <see langword="false"/> the gate always passes.
    /// Default <see langword="true"/>.
    /// </summary>
    public bool FailOnRegression { get; init; } = true;

    /// <summary>
    /// Gets the number of regressions tolerated before the gate fails. Default 0.
    /// </summary>
    public int MaxAllowedRegressions { get; init; } = 0;

    /// <summary>
    /// Gets the benchmark names excluded from regression counting. Each entry is matched ordinally against
    /// <see cref="BenchmarkComparison.FullName"/>; a trailing <c>*</c> turns the entry into a prefix match,
    /// e.g. <c>MyBench.Parse*</c>. Default is empty.
    /// </summary>
    public IReadOnlyList<string> IgnoredBenchmarks { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The outcome of evaluating the regression gate against a set of benchmark comparisons.
/// </summary>
/// <param name="Passed"><see langword="true"/> when the gate allows the run to proceed.</param>
/// <param name="Regressions">Comparisons that counted as regressions after ignored benchmarks were removed.</param>
/// <param name="Reason">Human-readable explanation of the outcome.</param>
public sealed record RegressionGateResult(bool Passed, IReadOnlyList<BenchmarkComparison> Regressions, string Reason);

/// <summary>
/// Decides whether a benchmark run passes or fails based on its regressed comparisons,
/// honouring <see cref="RegressionGateOptions"/>.
/// </summary>
public sealed class RegressionGate
{
    private readonly RegressionGateOptions _options;
    private readonly HashSet<string> _ignoredExact;
    private readonly IReadOnlyList<string> _ignoredPrefixes;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegressionGate"/> class.
    /// </summary>
    /// <param name="options">Gate options. Defaults are used when <see langword="null"/>.</param>
    public RegressionGate(RegressionGateOptions? options = null)
    {
        _options = options ?? new RegressionGateOptions();

        _ignoredExact = new HashSet<string>(StringComparer.Ordinal);
        var prefixes = new List<string>();

        foreach (var entry in _options.IgnoredBenchmarks)
        {
            if (entry is null)
            {
                continue;
            }

            if (entry.EndsWith('*'))
            {
                prefixes.Add(entry[..^1]);
            }
            else
            {
                _ignoredExact.Add(entry);
            }
        }

        _ignoredPrefixes = prefixes;
    }

    /// <summary>
    /// Evaluates the gate against the given comparisons. Only comparisons whose verdict is
    /// <see cref="RegressionVerdict.Regressed"/> and whose name is not ignored are counted.
    /// </summary>
    /// <param name="comparisons">Comparisons produced by the regression detector.</param>
    /// <returns>The gate result, including the counted regressions and a reason.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="comparisons"/> is <see langword="null"/>.</exception>
    public RegressionGateResult Evaluate(IReadOnlyList<BenchmarkComparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);

        var regressions = comparisons
            .Where(c => c.Verdict == RegressionVerdict.Regressed && !IsIgnored(c.FullName))
            .ToList();

        if (!_options.FailOnRegression)
        {
            return new RegressionGateResult(true, regressions, "regression gate disabled");
        }

        var passed = regressions.Count <= _options.MaxAllowedRegressions;

        var reason = (regressions.Count, passed) switch
        {
            (0, _) => "no regressions detected",
            (_, true) => string.Format(
                CultureInfo.InvariantCulture,
                "{0} regression(s) within the allowed maximum of {1}",
                regressions.Count,
                _options.MaxAllowedRegressions),
            _ => string.Format(
                CultureInfo.InvariantCulture,
                "{0} regression(s) exceed the allowed maximum of {1}",
                regressions.Count,
                _options.MaxAllowedRegressions),
        };

        return new RegressionGateResult(passed, regressions, reason);
    }

    /// <summary>
    /// Maps a gate result to a process exit code.
    /// </summary>
    /// <param name="result">The result returned by <see cref="Evaluate"/>.</param>
    /// <returns><c>0</c> when the gate passed, <c>1</c> otherwise.</returns>
    public int ToExitCode(RegressionGateResult result) => result.Passed ? 0 : 1;

    private bool IsIgnored(string fullName)
    {
        if (_ignoredExact.Contains(fullName))
        {
            return true;
        }

        foreach (var prefix in _ignoredPrefixes)
        {
            if (fullName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
