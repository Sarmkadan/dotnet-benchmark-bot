namespace DotnetBenchmarkBot;

/// <summary>
/// The outcome of a <see cref="BenchmarkPipeline.RunAsync"/> call.
/// </summary>
/// <param name="Comparisons">Per-benchmark comparisons produced by the regression detector.</param>
/// <param name="Markdown">Rendered report body produced by the formatter.</param>
/// <param name="CommentUrl">URL of the pull request comment that was created or updated, or <see langword="null"/> when no comment was posted.</param>
public sealed record BenchmarkPipelineResult(IReadOnlyList<BenchmarkComparison> Comparisons, string Markdown, Uri? CommentUrl);

/// <summary>
/// Runs the full benchmark regression pipeline: parse baseline and candidate reports, compare them,
/// format the result as Markdown, and optionally post it as a pull request comment.
/// </summary>
/// <remarks>
/// Each stage is supplied through its interface, so any stage can be replaced independently.
/// The comment stage is skipped when no <see cref="IPullRequestCommenter"/> is configured or when
/// <c>pullRequest</c> is <see langword="null"/> at call time.
/// </remarks>
public sealed class BenchmarkPipeline
{
    private readonly IBenchmarkReportParser _parser;
    private readonly IRegressionDetector _detector;
    private readonly IComparisonReportFormatter _formatter;
    private readonly IPullRequestCommenter? _commenter;

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkPipeline"/> class.
    /// </summary>
    /// <param name="parser">Parser that reads BenchmarkDotNet report directories.</param>
    /// <param name="detector">Detector that classifies each baseline/candidate pair.</param>
    /// <param name="formatter">Formatter that renders the comparisons as Markdown.</param>
    /// <param name="commenter">Optional commenter that posts the Markdown to a pull request; <see langword="null"/> disables commenting.</param>
    /// <exception cref="ArgumentNullException">A required stage (<paramref name="parser"/>, <paramref name="detector"/> or <paramref name="formatter"/>) is <see langword="null"/>.</exception>
    public BenchmarkPipeline(
        IBenchmarkReportParser parser,
        IRegressionDetector detector,
        IComparisonReportFormatter formatter,
        IPullRequestCommenter? commenter = null)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(formatter);

        _parser = parser;
        _detector = detector;
        _formatter = formatter;
        _commenter = commenter;
    }

    /// <summary>
    /// Parses both report directories, compares the results, formats them, and posts the report as a pull request comment
    /// when a commenter and a pull request reference are available.
    /// </summary>
    /// <param name="baselineDirectory">Path to the baseline <c>BenchmarkDotNet.Artifacts/results</c> directory.</param>
    /// <param name="candidateDirectory">Path to the candidate <c>BenchmarkDotNet.Artifacts/results</c> directory.</param>
    /// <param name="pullRequest">Pull request to comment on; <see langword="null"/> skips the comment stage.</param>
    /// <param name="thresholds">Regression thresholds; <see langword="null"/> uses the detector defaults.</param>
    /// <param name="formatOptions">Formatting options; <see langword="null"/> uses defaults. Its <see cref="ReportFormatOptions.Marker"/> is used to locate the existing comment.</param>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>
    /// The comparisons, the rendered Markdown, and the comment URL when one was posted; otherwise <see cref="BenchmarkPipelineResult.CommentUrl"/> is <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="baselineDirectory"/> or <paramref name="candidateDirectory"/> is <see langword="null"/>.</exception>
    public async Task<BenchmarkPipelineResult> RunAsync(
        string baselineDirectory,
        string candidateDirectory,
        PullRequestRef? pullRequest = null,
        RegressionThresholds? thresholds = null,
        ReportFormatOptions? formatOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baselineDirectory);
        ArgumentNullException.ThrowIfNull(candidateDirectory);

        var baselineTask = _parser.ParseDirectoryAsync(baselineDirectory, cancellationToken);
        var candidateTask = _parser.ParseDirectoryAsync(candidateDirectory, cancellationToken);
        await Task.WhenAll(baselineTask, candidateTask);

        var comparisons = _detector.Compare(baselineTask.Result, candidateTask.Result, thresholds);
        var markdown = _formatter.Format(comparisons, formatOptions);

        Uri? commentUrl = null;
        if (_commenter is not null && pullRequest is not null)
        {
            var marker = (formatOptions ?? new ReportFormatOptions()).Marker;
            commentUrl = await _commenter.UpsertCommentAsync(pullRequest, markdown, marker, cancellationToken);
        }

        return new BenchmarkPipelineResult(comparisons, markdown, commentUrl);
    }
}
