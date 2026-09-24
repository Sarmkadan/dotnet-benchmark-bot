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

## API overview

| Type | Role |
|------|------|
| `IBenchmarkReportParser` / `JsonBenchmarkReportParser` | Read BenchmarkDotNet JSON reports into `BenchmarkResult` |
| `IRegressionDetector` / `ThresholdRegressionDetector` | Pair baseline and candidate results, classify each with a `RegressionVerdict` |
| `RegressionThresholds` | Tunable ratios and noise sigma |
| `IComparisonReportFormatter` / `MarkdownComparisonReportFormatter` | Render comparisons as Markdown |
| `IPullRequestCommenter` / `GitHubPullRequestCommenter` | Create or update the PR comment via the GitHub REST API |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. See [LICENSE](LICENSE).
