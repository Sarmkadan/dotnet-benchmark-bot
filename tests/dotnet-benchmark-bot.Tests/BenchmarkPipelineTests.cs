namespace DotnetBenchmarkBot.Tests;

public class BenchmarkPipelineTests
{
    private const string BaselinePath = "/artifacts/baseline";
    private const string CandidatePath = "/artifacts/candidate";

    [Fact]
    public async Task RunAsync_ParsesBothDirectories_AndComparesTheUnionOfNames()
    {
        var parser = new FakeParser();
        parser.Add(BaselinePath, new BenchmarkResult("Bench.A", 100, 0, null), new BenchmarkResult("Bench.B", 100, 0, null));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.B", 100, 0, null), new BenchmarkResult("Bench.C", 100, 0, null));
        var pipeline = CreatePipeline(parser);

        var result = await pipeline.RunAsync(BaselinePath, CandidatePath);

        Assert.Equal(new[] { BaselinePath, CandidatePath }, parser.RequestedPaths.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(new[] { "Bench.A", "Bench.B", "Bench.C" }, result.Comparisons.Select(c => c.FullName));
    }

    [Fact]
    public async Task RunAsync_Markdown_StartsWithMarkerFromFormatOptions()
    {
        var parser = new FakeParser();
        parser.Add(BaselinePath, new BenchmarkResult("Bench.A", 100, 0, null));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.A", 150, 0, null));
        var pipeline = CreatePipeline(parser);
        var formatOptions = new ReportFormatOptions { Marker = "<!-- custom-marker -->" };

        var result = await pipeline.RunAsync(BaselinePath, CandidatePath, formatOptions: formatOptions);

        Assert.StartsWith(formatOptions.Marker, result.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithCommenterAndPullRequest_ReturnsCommentUrlAndPostsMarkdownWithMarker()
    {
        var parser = new FakeParser();
        parser.Add(BaselinePath, new BenchmarkResult("Bench.A", 100, 0, null));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.A", 150, 0, null));
        var commentUri = new Uri("https://github.com/owner/repo/pull/7#issuecomment-1");
        var commenter = new FakeCommenter(commentUri);
        var pipeline = CreatePipeline(parser, commenter);
        var pullRequest = new PullRequestRef("owner", "repo", 7);
        var formatOptions = new ReportFormatOptions { Marker = "<!-- custom-marker -->" };

        var result = await pipeline.RunAsync(BaselinePath, CandidatePath, pullRequest, formatOptions: formatOptions);

        Assert.Equal(commentUri, result.CommentUrl);
        Assert.Equal(1, commenter.CallCount);
        Assert.Same(pullRequest, commenter.LastPullRequest);
        Assert.Equal(result.Markdown, commenter.LastBody);
        Assert.Equal(formatOptions.Marker, commenter.LastMarker);
    }

    [Fact]
    public async Task RunAsync_WithoutCommenter_ReturnsNullCommentUrl()
    {
        var parser = new FakeParser();
        parser.Add(BaselinePath, new BenchmarkResult("Bench.A", 100, 0, null));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.A", 100, 0, null));
        var pipeline = CreatePipeline(parser, commenter: null);

        var result = await pipeline.RunAsync(BaselinePath, CandidatePath, new PullRequestRef("owner", "repo", 7));

        Assert.Null(result.CommentUrl);
        Assert.NotEmpty(result.Markdown);
    }

    [Fact]
    public async Task RunAsync_WithCommenterButNoPullRequest_DoesNotCallCommenter()
    {
        var parser = new FakeParser();
        parser.Add(BaselinePath, new BenchmarkResult("Bench.A", 100, 0, null));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.A", 100, 0, null));
        var commenter = new FakeCommenter(new Uri("https://github.com/owner/repo/pull/7#issuecomment-1"));
        var pipeline = CreatePipeline(parser, commenter);

        var result = await pipeline.RunAsync(BaselinePath, CandidatePath, pullRequest: null);

        Assert.Null(result.CommentUrl);
        Assert.Equal(0, commenter.CallCount);
    }

    [Fact]
    public async Task RunAsync_ForwardsThresholds_ToDetector()
    {
        // 30% slowdown with no noise: regressed under the default 10% limit, unchanged under a 50% limit.
        var parser = new FakeParser();
        parser.Add(BaselinePath, new BenchmarkResult("Bench.A", 100, 0, null));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.A", 130, 0, null));
        var pipeline = CreatePipeline(parser);

        var defaultResult = await pipeline.RunAsync(BaselinePath, CandidatePath);
        var relaxedResult = await pipeline.RunAsync(
            BaselinePath,
            CandidatePath,
            thresholds: new RegressionThresholds { MeanRegressionRatio = 0.5 });

        Assert.Equal(RegressionVerdict.Regressed, defaultResult.Comparisons.Single().Verdict);
        Assert.Equal(RegressionVerdict.Unchanged, relaxedResult.Comparisons.Single().Verdict);
    }

    [Fact]
    public async Task RunAsync_WhenParserThrowsDirectoryNotFound_PropagatesException()
    {
        var parser = new FakeParser();
        parser.Throw(BaselinePath, new DirectoryNotFoundException("missing baseline"));
        parser.Add(CandidatePath, new BenchmarkResult("Bench.A", 100, 0, null));
        var pipeline = CreatePipeline(parser);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => pipeline.RunAsync(BaselinePath, CandidatePath));
    }

    [Fact]
    public void RegressionGate_OneRegressionWithDefaults_Fails()
    {
        var gate = new RegressionGate();

        var result = gate.Evaluate([Regressed("Bench.A")]);

        Assert.False(result.Passed);
        Assert.Single(result.Regressions);
    }

    [Fact]
    public void RegressionGate_FailOnRegressionDisabled_Passes()
    {
        var gate = new RegressionGate(new RegressionGateOptions { FailOnRegression = false });

        var result = gate.Evaluate([Regressed("Bench.A")]);

        Assert.True(result.Passed);
    }

    [Fact]
    public void RegressionGate_IgnoredPrefix_ExcludesMatchingRegression()
    {
        var gate = new RegressionGate(new RegressionGateOptions { IgnoredBenchmarks = ["A.B.Run*"] });

        var result = gate.Evaluate([Regressed("A.B.Run(size: 1)")]);

        Assert.True(result.Passed);
        Assert.Empty(result.Regressions);
    }

    private static BenchmarkComparison Regressed(string fullName) =>
        new(fullName, null, null, 1.5, null, RegressionVerdict.Regressed);

    private static BenchmarkPipeline CreatePipeline(FakeParser parser, IPullRequestCommenter? commenter = null) =>
        new(parser, new ThresholdRegressionDetector(), new MarkdownComparisonReportFormatter(), commenter);

    private sealed class FakeParser : IBenchmarkReportParser
    {
        private readonly Dictionary<string, IReadOnlyList<BenchmarkResult>> _results = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Exception> _exceptions = new(StringComparer.Ordinal);
        private readonly List<string> _requestedPaths = new();

        public IReadOnlyList<string> RequestedPaths => _requestedPaths;

        public void Add(string path, params BenchmarkResult[] results) => _results[path] = results;

        public void Throw(string path, Exception exception) => _exceptions[path] = exception;

        public Task<IReadOnlyList<BenchmarkResult>> ParseAsync(Stream reportStream, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The pipeline only parses directories.");

        public Task<IReadOnlyList<BenchmarkResult>> ParseDirectoryAsync(string artifactsDirectory, CancellationToken cancellationToken = default)
        {
            _requestedPaths.Add(artifactsDirectory);

            if (_exceptions.TryGetValue(artifactsDirectory, out var exception))
            {
                return Task.FromException<IReadOnlyList<BenchmarkResult>>(exception);
            }

            if (!_results.TryGetValue(artifactsDirectory, out var results))
            {
                return Task.FromException<IReadOnlyList<BenchmarkResult>>(
                    new DirectoryNotFoundException($"No preconfigured results for '{artifactsDirectory}'."));
            }

            return Task.FromResult(results);
        }
    }

    private sealed class FakeCommenter : IPullRequestCommenter
    {
        private readonly Uri _resultUri;

        public FakeCommenter(Uri resultUri) => _resultUri = resultUri;

        public int CallCount { get; private set; }

        public PullRequestRef? LastPullRequest { get; private set; }

        public string? LastBody { get; private set; }

        public string? LastMarker { get; private set; }

        public Task<Uri> UpsertCommentAsync(PullRequestRef pullRequest, string body, string marker, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPullRequest = pullRequest;
            LastBody = body;
            LastMarker = marker;
            return Task.FromResult(_resultUri);
        }
    }
}
