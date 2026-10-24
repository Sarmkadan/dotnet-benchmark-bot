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

## Using in GitHub Actions

The workflow below benchmarks the base branch and the PR head, then hands both result folders to the bot. It runs on `pull_request`, so the comment is posted on the PR itself.

```yaml
name: Benchmarks

on:
  pull_request:
    branches: [main]

jobs:
  benchmarks:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      pull-requests: write
    env:
      GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
    steps:
      - uses: actions/checkout@v4
        with:
          ref: ${{ github.event.pull_request.head.sha }}

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      # Benchmark the base branch in a separate worktree next to the checkout.
      - name: Benchmark base branch
        run: |
          git fetch origin ${{ github.base_ref }}
          git worktree add ../base origin/${{ github.base_ref }}
          dotnet run -c Release --project ../base/benchmarks -- --exporters json --artifacts artifacts/baseline

      # Benchmark the PR head, which is the current checkout.
      - name: Benchmark PR head
        run: dotnet run -c Release --project benchmarks -- --exporters json --artifacts artifacts/pr

      # Coming soon: no dotnet-benchmark-bot CLI is published yet, so this step will fail until it ships.
      # Until then, call the library from a small console project (see Quick start).
      - name: Compare and comment (coming soon)
        run: >
          dotnet benchmark-bot compare
          --baseline artifacts/baseline/results
          --candidate artifacts/pr/results
          --pr ${{ github.repository }}#${{ github.event.pull_request.number }}
```

`--exporters json` writes the `*-report-full.json` files the parser reads. `--artifacts` places them under `results/` inside the folder you pass, which is why the bot paths end in `results`.

### Environment discovery

`GitHubActionsContext.FromEnvironment()` builds a context from the runner's environment variables. It reads:

| Variable | Used for |
|----------|----------|
| `GITHUB_ACTIONS` | `IsGitHubActions`, true when the value is `true` |
| `GITHUB_TOKEN` | `Token`, or `null` when unset or empty |
| `GITHUB_REPOSITORY` | Owner and repository of `PullRequest` (`owner/repo`) |
| `GITHUB_REF` | Pull request number when the ref is `refs/pull/{number}/merge` |
| `GITHUB_EVENT_PATH` | Fallback number from the event payload (`pull_request.number`, or a top-level `number`) |

`PullRequest` is `null` unless the owner, repository and a positive number are all found.

```csharp
using DotnetBenchmarkBot;

var context = GitHubActionsContext.FromEnvironment();
if (context.Token is null || context.PullRequest is null)
{
    Console.WriteLine("No pull request or token found, skipping the comment.");
    return;
}
```

### Failing the build on regressions

`RegressionGate` evaluates the comparisons from `ThresholdRegressionDetector.Compare` and returns a `RegressionGateResult` with `Passed`, the counted `Regressions` and a `Reason`. Only `Regressed` verdicts count, and ignored benchmarks are removed before counting.

| `RegressionGateOptions` property | Default | Meaning |
|----------------------------------|---------|---------|
| `FailOnRegression` | `true` | When `false`, the gate always passes. Regressions are still reported |
| `MaxAllowedRegressions` | `0` | How many counted regressions are tolerated before the gate fails |
| `IgnoredBenchmarks` | empty | Full names to skip. An entry matches exactly, or as a prefix when it ends with `*`, e.g. `MyBenchmarks.Parse*` |

```csharp
using DotnetBenchmarkBot;

var comparisons = new ThresholdRegressionDetector().Compare(baseline, candidate, thresholds);

var gate = new RegressionGate(new RegressionGateOptions
{
    MaxAllowedRegressions = 1,
    IgnoredBenchmarks = new[] { "MyBenchmarks.Serialize*" },
});

var result = gate.Evaluate(comparisons);
Console.WriteLine(result.Reason);
return gate.ToExitCode(result); // 0 when passed, 1 otherwise
```

Returning a non-zero exit code fails the job, which fails the workflow run.

## Troubleshooting

- **Comment not updated (marker mismatch).** The bot finds its comment by searching existing comment bodies for `Marker` (default `<!-- dotnet-benchmark-bot -->`). If the marker changed between runs, or the marker line was removed from the comment, a new comment is posted instead of updating the old one. Keep `Marker` stable, and restore the line or delete the stale comment.
- **403 from GitHub (missing `pull-requests: write`).** The comment is written with `GITHUB_TOKEN`, which needs `pull-requests: write` on the job, as in the workflow above. Runs from forked PRs get a read-only token, so they return 403 even when the permission is set.
- **No reports found (need `--exporters json`, path must be the `results` folder).** The parser only reads `*-report-full.json` files, and only from the directory you pass. Run the benchmarks with `--exporters json`, and point the baseline and candidate paths at `artifacts/baseline/results` and `artifacts/pr/results`, not at `artifacts/baseline` or `artifacts/pr`.

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
