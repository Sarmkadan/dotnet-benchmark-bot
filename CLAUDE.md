# dotnet-benchmark-bot

GitHub Action and NuGet tool that runs BenchmarkDotNet on PRs and posts regression comments.

## Build

```bash
dotnet restore
dotnet build -c Release
dotnet pack src/dotnet-benchmark-bot -c Release -o artifacts
```

## Test

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~PlaceholderTests"
```

## Architecture

Pipeline: parse -> compare -> format -> comment.

1. `IBenchmarkReportParser` reads BenchmarkDotNet `*-report-full.json` into `BenchmarkResult` records.
2. `IRegressionDetector` pairs baseline and candidate results by `FullName` and assigns a `RegressionVerdict` using `RegressionThresholds` (ratio + sigma noise gate).
3. `IComparisonReportFormatter` renders `BenchmarkComparison` rows as GitHub Markdown with a hidden marker.
4. `IPullRequestCommenter` finds an existing comment by marker and updates it, otherwise creates one.

Each stage is an interface with one default implementation so a CLI (`dotnet tool`) and a GitHub Action can compose them.

## Key directories

- `src/dotnet-benchmark-bot/` - library, namespace `DotnetBenchmarkBot`, PackageId `DotnetBenchmarkBot`
- `tests/dotnet-benchmark-bot.Tests/` - xUnit tests
- `.github/workflows/ci.yml` - build + test on push/PR
- `Directory.Build.props` - shared compiler settings

## Conventions

- net8.0, `Nullable` enabled, `LangVersion` latest, `TreatWarningsAsErrors` true.
- Every public type and member has XML docs (`GenerateDocumentationFile` is on, missing docs fail the build).
- Records for immutable data (`BenchmarkResult`, `BenchmarkComparison`, `PullRequestRef`), classes with `init` props for options.
- Async methods take a trailing `CancellationToken cancellationToken = default`.
- Stubs throw `NotImplementedException`; replace them with real code and add tests in the same commit.
- No `Co-Authored-By` in commits. Conventional commit prefixes: `feat:`, `fix:`, `test:`, `docs:`, `chore:`.
