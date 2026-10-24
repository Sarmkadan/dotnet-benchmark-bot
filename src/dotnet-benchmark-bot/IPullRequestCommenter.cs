using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetBenchmarkBot;

/// <summary>
/// Identifies a pull request on a GitHub repository.
/// </summary>
public sealed record PullRequestRef
{
    /// <summary>
    /// Creates a validated reference to a pull request.
    /// </summary>
    /// <param name="owner">Repository owner (user or organization). Must be non-empty and must not contain <c>/</c>.</param>
    /// <param name="repository">Repository name. Must be non-empty and must not contain <c>/</c>.</param>
    /// <param name="number">Pull request number. Must be at least 1.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="repository"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="repository"/> is empty, whitespace, or contains <c>/</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is less than 1.</exception>
    public PullRequestRef(string owner, string repository, int number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        if (owner.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Owner must not contain '/'.", nameof(owner));
        }

        if (repository.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Repository must not contain '/'.", nameof(repository));
        }

        Owner = owner;
        Repository = repository;
        Number = number;
    }

    /// <summary>
    /// Repository owner (user or organization).
    /// </summary>
    public string Owner { get; init; }

    /// <summary>
    /// Repository name.
    /// </summary>
    public string Repository { get; init; }

    /// <summary>
    /// Pull request number.
    /// </summary>
    public int Number { get; init; }
}

/// <summary>
/// Posts or updates a benchmark comparison comment on a pull request.
/// </summary>
public interface IPullRequestCommenter
{
    /// <summary>
    /// Creates a comment, or updates the existing bot comment when one containing <paramref name="marker"/> is found.
    /// </summary>
    /// <param name="pullRequest">Target pull request.</param>
    /// <param name="body">Markdown body produced by <see cref="IComparisonReportFormatter"/>.</param>
    /// <param name="marker">Hidden marker used to locate a previous comment from this bot.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The URL of the created or updated comment.</returns>
    Task<Uri> UpsertCommentAsync(PullRequestRef pullRequest, string body, string marker, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IPullRequestCommenter"/> backed by the GitHub REST API (issues comments endpoint).
/// </summary>
public sealed class GitHubPullRequestCommenter : IPullRequestCommenter
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a commenter that authenticates with a GitHub token (typically <c>GITHUB_TOKEN</c>).
    /// </summary>
    /// <param name="httpClient">Client used for API calls. Base address defaults to <c>https://api.github.com</c> when unset.</param>
    /// <param name="token">Personal access token or Actions token with <c>pull-requests: write</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="token"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public GitHubPullRequestCommenter(HttpClient httpClient, string token)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri("https://api.github.com/");

        var headers = _httpClient.DefaultRequestHeaders;
        if (!headers.Contains("Accept"))
        {
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        if (!headers.Contains("X-GitHub-Api-Version"))
        {
            headers.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        if (!headers.Contains("User-Agent"))
        {
            var version = typeof(GitHubPullRequestCommenter).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            headers.UserAgent.Add(new ProductInfoHeaderValue("dotnet-benchmark-bot", version));
        }

        if (headers.Authorization is null)
        {
            headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="pullRequest"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="body"/> or <paramref name="marker"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public Task<Uri> UpsertCommentAsync(PullRequestRef pullRequest, string body, string marker, CancellationToken cancellationToken = default)
    {
        // Validate synchronously so bad arguments throw at the call site rather than inside the returned task.
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        return UpsertCoreAsync(pullRequest, body, marker, cancellationToken);
    }

    private async Task<Uri> UpsertCoreAsync(PullRequestRef pullRequest, string body, string marker, CancellationToken cancellationToken)
    {
        // Keep the marker in the body so the next run can find this comment again.
        var payloadBody = body.Contains(marker, StringComparison.Ordinal) ? body : marker + "\n" + body;

        var existing = await FindExistingCommentAsync(pullRequest, marker, cancellationToken);
        var comment = existing is null
            ? await SendAsync<IssueComment>(HttpMethod.Post, $"repos/{pullRequest.Owner}/{pullRequest.Repository}/issues/{pullRequest.Number}/comments", payloadBody, cancellationToken)
            : await SendAsync<IssueComment>(HttpMethod.Patch, $"repos/{pullRequest.Owner}/{pullRequest.Repository}/issues/comments/{existing.Id}", payloadBody, cancellationToken);

        return new Uri(comment.HtmlUrl);
    }

    private async Task<IssueComment?> FindExistingCommentAsync(PullRequestRef pullRequest, string marker, CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var commentsUrl = $"repos/{pullRequest.Owner}/{pullRequest.Repository}/issues/{pullRequest.Number}/comments";

        for (var page = 1; ; page++)
        {
            var comments = await SendAsync<List<IssueComment>>(HttpMethod.Get, $"{commentsUrl}?per_page={pageSize}&page={page}", null, cancellationToken);

            var match = comments.FirstOrDefault(c => (c.Body ?? string.Empty).Contains(marker, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            if (comments.Count < pageSize)
            {
                return null;
            }
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, string? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(new { body }, options: s_json);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<T>(s_json, cancellationToken);
            return result ?? throw new InvalidOperationException($"GitHub API returned an empty body for {method} {url}.");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"GitHub API request {method} {url} failed with status {(int?)ex.StatusCode}: {ex.Message}", ex, ex.StatusCode);
        }
    }

    private sealed record IssueComment(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
