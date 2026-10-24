using System.Net.Http.Headers;
using System.Text.Json;

namespace DotnetBenchmarkBot;

/// <summary>
/// Identifies a pull request on a GitHub repository.
/// </summary>
/// <param name="Owner">Repository owner (user or organization).</param>
/// <param name="Repository">Repository name.</param>
/// <param name="Number">Pull request number.</param>
public sealed record PullRequestRef(string Owner, string Repository, int Number);

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
    public GitHubPullRequestCommenter(HttpClient httpClient, string token)
    {
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
    public Task<Uri> UpsertCommentAsync(PullRequestRef pullRequest, string body, string marker, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
