# dotnet-benchmark-bot

[![CI](https://github.com/Sarmkadan/dotnet-benchmark-bot/actions/workflows/ci.yml/badge.svg)](https://github.com/Sarmkadan/dotnet-benchmark-bot/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DotnetBenchmarkBot.svg)](https://www.nuget.org/packages/DotnetBenchmarkBot)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

GitHub Action and NuGet tool that runs BenchmarkDotNet on pull requests and posts regression comments.

Run your benchmarks on the base branch and the PR head, compare the two, and get a Markdown table on the PR telling you exactly which benchmarks got slower, faster, or started allocating more.

## Features

- Parses BenchmarkDotNet JSON exporter output (`*-report-full.json`)
- Matches baseline and candidate runs by benchmark name
- Ratio-based regression detection with a sigma noise gate to avoid false alarms
- Tracks both mean time and allocated bytes
- Renders a GitHub-flavored Markdown report with collapsible tables
- Upserts a single PR comment instead of spamming one per run
- Usable as a GitHub Action, a `dotnet tool`, or a library

## Quick start

### As a library

```csharp
using DotnetBenchmarkBot;

var parser = new JsonBenchmarkReportParser();
var baseline = await parser.ParseDirectoryAsync("artifacts/baseline/results");
var candidate = await parser.ParseDirectoryAsync("artifacts/pr/results");

var detector = new ThresholdRegressionDetector();
var comparisons = detector.Compare(baseline, candidate, new RegressionThresholds
{
    MeanRegressionRatio = 0.10,
    AllocationRegressionRatio = 0.05,
});

var markdown = new MarkdownComparisonReportFormatter().Format(comparisons);

var commenter = new GitHubPullRequestCommenter(new HttpClient(), Environment.GetEnvironmentVariable("GITHUB_TOKEN")!);
await commenter.UpsertCommentAsync(
    new PullRequestRef("Sarmkadan", "dotnet-benchmark-bot", 42),
    markdown,
    marker: "<!-- dotnet-benchmark-bot -->");
```

#### Run the whole pipeline

`BenchmarkPipeline` chains the parser, detector, formatter and an optional commenter. The commenter is skipped when none is passed or when `pullRequest` is `null`.

```csharp
using DotnetBenchmarkBot;

var pipeline = new BenchmarkPipeline(
    new JsonBenchmarkReportParser(),
    new ThresholdRegressionDetector(),
    new MarkdownComparisonReportFormatter(),
    new GitHubPullRequestCommenter(new HttpClient(), Environment.GetEnvironmentVariable("GITHUB_TOKEN")!));

var result = await pipeline.RunAsync(
    "artifacts/baseline/results",
    "artifacts/pr/results",
    new PullRequestRef("Sarmkadan", "dotnet-benchmark-bot", 42),
    new RegressionThresholds { MeanRegressionRatio = 0.10, AllocationRegressionRatio = 0.05 });

Console.WriteLine(result.CommentUrl?.ToString() ?? "no comment posted");

// Without a commenter or a pull request, the pipeline only compares and formats.
var offline = new BenchmarkPipeline(
    new JsonBenchmarkReportParser(),
    new ThresholdRegressionDetector(),
    new MarkdownComparisonReportFormatter());

var report = await offline.RunAsync("artifacts/baseline/results", "artifacts/pr/results");
Console.WriteLine(report.Markdown);
```

#### Register with dependency injection

`AddDotnetBenchmarkBot` registers the default stages, `BenchmarkPipeline` and `BenchmarkBotOptions`. `IPullRequestCommenter` is registered only when `GitHubToken` is set. The consuming project needs the `Microsoft.Extensions.DependencyInjection` package for `ServiceCollection`.

```csharp
using DotnetBenchmarkBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var services = new ServiceCollection();
services.AddDotnetBenchmarkBot(options =>
{
    options.BaselineDirectory = "artifacts/baseline/results";
    options.CandidateDirectory = "artifacts/pr/results";
    options.GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    options.PullRequest = "Sarmkadan/dotnet-benchmark-bot#42";
});

await using var provider = services.BuildServiceProvider();
var pipeline = provider.GetRequiredService<BenchmarkPipeline>();
var options = provider.GetRequiredService<IOptions<BenchmarkBotOptions>>().Value;

PullRequestRef? pullRequest = null;
if (options.PullRequest is not null)
{
    GitHubActionsContext.TryParsePullRequestRef(options.PullRequest, out pullRequest);
}

var result = await pipeline.RunAsync(
    options.BaselineDirectory,
    options.CandidateDirectory,
    pullRequest,
    options.Thresholds,
    options.Report);

Console.WriteLine(result.CommentUrl?.ToString() ?? "no comment posted");
```

### As a GitHub Action (planned)

```yaml
- uses: Sarmkadan/dotnet-benchmark-bot@v1
  with:
    baseline: artifacts/baseline/results
    candidate: artifacts/pr/results
    mean-threshold: 0.10
    github-token: ${{ secrets.GITHUB_TOKEN }}
```

## Installation

```bash
dotnet add package DotnetBenchmarkBot
```

## Regression detection

`ThresholdRegressionDetector` pairs baseline and candidate benchmarks by `FullName` and classifies each pair using `RegressionThresholds`. Passing `null` uses the defaults below. `RegressionThresholds.Validate()` throws `ArgumentOutOfRangeException` for negative or NaN values.

| Property | Default | Meaning |
|----------|---------|---------|
| `MeanRegressionRatio` | `0.10` | Relative mean-time increase (candidate / baseline - 1) above which a trusted delta is `Regressed` |
| `MeanImprovementRatio` | `0.10` | Relative mean-time decrease (1 - candidate / baseline) above which a trusted delta is `Improved` |
| `AllocationRegressionRatio` | `0.05` | Relative change in allocated bytes that triggers a verdict. `null` disables allocation checks. Not subject to the noise gate |
| `NoiseSigma` | `2.0` | Number of standard deviations a mean delta must exceed before it is trusted |

**Sigma noise gate.** A time delta is trusted only when `|candidate mean - baseline mean| > NoiseSigma * max(baseline stddev, candidate stddev)`. Deltas inside that band are noise: their mean ratio is ignored, even when it exceeds a threshold. Allocation counts are deterministic, so they bypass the gate.

`RegressionVerdict` values:

| Verdict | Meaning |
|---------|---------|
| `Regressed` | Allocations grew beyond `AllocationRegressionRatio`, or a trusted mean slowdown exceeded `MeanRegressionRatio` |
| `Improved` | A trusted mean speedup exceeded `MeanImprovementRatio`, or allocations shrank beyond `AllocationRegressionRatio` while time was unchanged |
| `Unchanged` | Within thresholds |
| `Added` | Present only in the candidate run |
| `Removed` | Present only in the baseline run |

An allocation regression is reported even when the time delta is not trusted. A time verdict takes precedence over an allocation improvement.

## Report format

`MarkdownComparisonReportFormatter` renders a summary line and a results table. `ReportFormatOptions` controls the output. Passing `null` uses the defaults below.

| Property | Default | Meaning |
|----------|---------|---------|
| `HideUnchanged` | `true` | Omit `Unchanged` rows from the table |
| `CollapseAfterRows` | `25` | When more rows remain than this, `Regressed` rows stay in a `### Regressions` table and the rest move into a `<details>` block |
| `Title` | `null` | Optional `##` heading rendered above the summary |
| `Marker` | `<!-- dotnet-benchmark-bot -->` | Hidden first line used to find and update the existing PR comment. Keep it stable between runs |

Sample output from `Format(comparisons, new ReportFormatOptions { Title = "Benchmark comparison" })`, with one regression, one improvement and one unchanged benchmark (hidden by default). `Ratio` is the percent change in mean time:

```markdown
<!-- dotnet-benchmark-bot -->
## Benchmark comparison

**Benchmarks:** 3 total, 1 regressed :red_circle:, 1 improved :green_circle:, 1 unchanged, 0 added, 0 removed

| Benchmark | Baseline | Candidate | Ratio | Alloc | Verdict |
| --- | ---: | ---: | ---: | ---: | --- |
| `MyBenchmarks.Parse(size: 1024)` | 1.2 us | 1.5 us | +25.0% | 0 B | :red_circle: Regressed |
| `MyBenchmarks.Serialize(size: 1024)` | 800 ns | 600 ns | -25.0% | 0 B | :green_circle: Improved |
```

## API overview

| Type | Role |
|------|------|
| `IBenchmarkReportParser` / `JsonBenchmarkReportParser` | Read BenchmarkDotNet JSON reports into `BenchmarkResult` |
| `IRegressionDetector` / `ThresholdRegressionDetector` | Pair baseline and candidate results, classify each with a `RegressionVerdict` |
| `RegressionThresholds` | Tunable ratios and noise sigma |
| `IComparisonReportFormatter` / `MarkdownComparisonReportFormatter` | Render comparisons as Markdown |
| `ReportFormatOptions` | Options for the Markdown formatter: hiding, collapsing, title and marker |
| `IPullRequestCommenter` / `GitHubPullRequestCommenter` | Create or update the PR comment via the GitHub REST API |
| `BenchmarkPipeline` / `BenchmarkPipelineResult` | Run parse, compare, format and optional comment in one `RunAsync` call |
| `ServiceCollectionExtensions.AddDotnetBenchmarkBot` | Register the pipeline, default stages and `BenchmarkBotOptions` with DI |
| `BenchmarkBotOptions` | Root configuration: directories, thresholds, report options, GitHub token and PR |
| `GitHubActionsContext` | Discover the PR and token from GitHub Actions environment variables |
| `RegressionGate` / `RegressionGateOptions` / `RegressionGateResult` | Fail the run when regressions exceed an allowed count, with ignore patterns |
| `BenchmarkComparisonSummary` | Count verdicts and render the one-line summary |
| `BenchmarkNameParser` / `BenchmarkName` | Split a benchmark full name into type, method and parameters |
| `DurationFormatter` | Format nanosecond durations and ratios for reports |
| `ByteSizeFormatter` | Format byte counts and allocation deltas for reports |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. See [LICENSE](LICENSE).
