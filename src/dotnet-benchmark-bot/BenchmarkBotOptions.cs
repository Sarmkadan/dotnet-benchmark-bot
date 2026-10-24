using System.Text;
using System.Text.RegularExpressions;

namespace DotnetBenchmarkBot;

/// <summary>
/// Root configuration for the benchmark bot, bound from the <see cref="SectionName"/> configuration section.
/// </summary>
public sealed class BenchmarkBotOptions
{
    /// <summary>
    /// Name of the configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "BenchmarkBot";

    private static readonly Regex PullRequestPattern = new(
        @"^[^/\s]+/[^/\s#]+#[1-9][0-9]*$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Directory containing the baseline BenchmarkDotNet <c>*-report-full.json</c> files.
    /// Default <c>BenchmarkDotNet.Artifacts/baseline/results</c>.
    /// </summary>
    public string BaselineDirectory { get; set; } = "BenchmarkDotNet.Artifacts/baseline/results";

    /// <summary>
    /// Directory containing the candidate BenchmarkDotNet <c>*-report-full.json</c> files.
    /// Default <c>BenchmarkDotNet.Artifacts/results</c>.
    /// </summary>
    public string CandidateDirectory { get; set; } = "BenchmarkDotNet.Artifacts/results";

    /// <summary>
    /// Thresholds that decide when a benchmark delta counts as a regression or improvement.
    /// </summary>
    public RegressionThresholds Thresholds { get; set; } = new();

    /// <summary>
    /// Options that control how the comparison report is rendered.
    /// </summary>
    public ReportFormatOptions Report { get; set; } = new();

    /// <summary>
    /// GitHub token used to read and write pull request comments. <see langword="null"/> when not configured.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// Target pull request in <c>owner/repo#42</c> form. <see langword="null"/> when not configured.
    /// </summary>
    public string? PullRequest { get; set; }

    /// <summary>
    /// Checks every option and reports all problems at once.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// One or more options are invalid. The message lists every problem, one per line.
    /// </exception>
    public void Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(BaselineDirectory))
        {
            problems.Add($"{nameof(BaselineDirectory)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(CandidateDirectory))
        {
            problems.Add($"{nameof(CandidateDirectory)} must not be empty.");
        }

        try
        {
            Thresholds.Validate();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            problems.Add($"{nameof(Thresholds)}: {ex.Message}");
        }

        if (Report.CollapseAfterRows < 1)
        {
            problems.Add($"{nameof(Report)}.{nameof(ReportFormatOptions.CollapseAfterRows)} must be at least 1 (was {Report.CollapseAfterRows}).");
        }

        if (PullRequest is not null && !PullRequestPattern.IsMatch(PullRequest))
        {
            problems.Add($"{nameof(PullRequest)} must be in 'owner/repo#N' form (was '{PullRequest}').");
        }

        if (problems.Count == 0)
        {
            return;
        }

        var message = new StringBuilder("Invalid BenchmarkBot options:");
        foreach (var problem in problems)
        {
            message.Append(Environment.NewLine).Append("  - ").Append(problem);
        }

        throw new InvalidOperationException(message.ToString());
    }
}
