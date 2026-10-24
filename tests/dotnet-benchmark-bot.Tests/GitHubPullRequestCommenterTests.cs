using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DotnetBenchmarkBot.Tests;

public class GitHubPullRequestCommenterTests
{
    private const string Marker = "<!-- m -->";

    [Fact]
    public async Task UpsertCommentAsync_NoExistingComments_PostsNewComment()
    {
        var handler = new StubHandler();
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.Created, """{"id":99,"body":"x","html_url":"https://github.com/o/r/pull/7#issuecomment-99"}"""));
        using var httpClient = new HttpClient(handler);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");
        const string markdown = Marker + "\n## Benchmarks\n";

        var uri = await commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), markdown, Marker);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("/repos/o/r/issues/7/comments", handler.Requests[1].Path);
        Assert.Equal(markdown, ReadBodyProperty(handler.Requests[1].Body));
        Assert.Equal(new Uri("https://github.com/o/r/pull/7#issuecomment-99"), uri);
    }

    [Fact]
    public async Task UpsertCommentAsync_ExistingCommentWithMarker_PatchesThatComment()
    {
        var handler = new StubHandler();
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.OK, """[{"id":11,"body":"<!-- m -->old","html_url":"https://github.com/o/r/pull/7#issuecomment-11"}]"""));
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.OK, """{"id":11,"body":"<!-- m -->new","html_url":"https://github.com/o/r/pull/7#issuecomment-11"}"""));
        using var httpClient = new HttpClient(handler);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");
        const string markdown = Marker + "\nnew results";

        var uri = await commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), markdown, Marker);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
        Assert.Equal("/repos/o/r/issues/comments/11", handler.Requests[1].Path);
        Assert.Equal(markdown, ReadBodyProperty(handler.Requests[1].Body));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(new Uri("https://github.com/o/r/pull/7#issuecomment-11"), uri);
    }

    [Fact]
    public async Task UpsertCommentAsync_ExistingCommentsWithoutMarker_PostsNewComment()
    {
        var handler = new StubHandler();
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.OK, """
            [
              {"id":5,"body":"looks good to me","html_url":"https://github.com/o/r/pull/7#issuecomment-5"},
              {"id":6,"body":null,"html_url":"https://github.com/o/r/pull/7#issuecomment-6"}
            ]
            """));
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.Created, """{"id":12,"body":"x","html_url":"https://github.com/o/r/pull/7#issuecomment-12"}"""));
        using var httpClient = new HttpClient(handler);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");

        var uri = await commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), Marker + "\nbody", Marker);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("/repos/o/r/issues/7/comments", handler.Requests[1].Path);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Equal(new Uri("https://github.com/o/r/pull/7#issuecomment-12"), uri);
    }

    [Fact]
    public async Task UpsertCommentAsync_SetsAuthorizationAcceptAndUserAgentHeaders()
    {
        var handler = new StubHandler();
        HttpRequestHeaders? captured = null;
        handler.Enqueue(req =>
        {
            captured = req.Headers;
            return JsonResponse(HttpStatusCode.OK, "[]");
        });
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.Created, """{"id":1,"body":"x","html_url":"https://github.com/o/r/pull/7#issuecomment-1"}"""));
        using var httpClient = new HttpClient(handler);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");

        await commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), Marker + "\nbody", Marker);

        Assert.NotNull(captured);
        Assert.NotNull(captured.Authorization);
        Assert.Equal("Bearer", captured.Authorization.Scheme);
        Assert.Equal("tok", captured.Authorization.Parameter);
        Assert.Contains(captured.Accept, h => h.MediaType == "application/vnd.github+json");
        Assert.Contains("dotnet-benchmark-bot", captured.UserAgent.ToString());
    }

    [Fact]
    public async Task UpsertCommentAsync_ForbiddenOnList_ThrowsHttpRequestException()
    {
        var handler = new StubHandler();
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.Forbidden, """{"message":"Resource not accessible by integration"}"""));
        using var httpClient = new HttpClient(handler);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), Marker + "\nbody", Marker));

        Assert.Contains("403", ex.Message);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task UpsertCommentAsync_BodyWithoutMarker_PostsBodyContainingMarker()
    {
        var handler = new StubHandler();
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.Created, """{"id":2,"body":"x","html_url":"https://github.com/o/r/pull/7#issuecomment-2"}"""));
        using var httpClient = new HttpClient(handler);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");

        await commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), "## Results", Marker);

        var posted = ReadBodyProperty(handler.Requests[1].Body);
        Assert.Equal(Marker + "\n## Results", posted);
        Assert.Contains(Marker, posted);
    }

    [Fact]
    public async Task UpsertCommentAsync_BaseAddressUnset_DefaultsToGitHubApi()
    {
        var handler = new StubHandler();
        Uri? requested = null;
        handler.Enqueue(req =>
        {
            requested = req.RequestUri;
            return JsonResponse(HttpStatusCode.OK, "[]");
        });
        handler.Enqueue(_ => JsonResponse(HttpStatusCode.Created, """{"id":3,"body":"x","html_url":"https://github.com/o/r/pull/7#issuecomment-3"}"""));
        using var httpClient = new HttpClient(handler);
        Assert.Null(httpClient.BaseAddress);
        var commenter = new GitHubPullRequestCommenter(httpClient, "tok");

        Assert.Equal(new Uri("https://api.github.com/"), httpClient.BaseAddress);

        await commenter.UpsertCommentAsync(new PullRequestRef("o", "r", 7), Marker + "\nbody", Marker);

        Assert.NotNull(requested);
        Assert.Equal("https", requested.Scheme);
        Assert.Equal("api.github.com", requested.Host);
        Assert.StartsWith("/repos/o/r/issues/7/comments", requested.AbsolutePath, StringComparison.Ordinal);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string? ReadBodyProperty(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("body").GetString();
    }
}

/// <summary>
/// Records every request and answers each one from a queue of responders, so tests run without network access.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    /// <summary>
    /// Requests seen so far, in order: method, path plus query, and body text (empty when there is no body).
    /// </summary>
    public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = new();

    /// <summary>
    /// Queues the responder used for the next request.
    /// </summary>
    /// <param name="responder">Builds the response for one request.</param>
    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responders.Enqueue(responder);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));

        if (_responders.Count == 0)
        {
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        }

        return _responders.Dequeue()(request);
    }
}
