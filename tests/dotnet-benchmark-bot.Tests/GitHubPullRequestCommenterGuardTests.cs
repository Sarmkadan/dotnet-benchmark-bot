namespace DotnetBenchmarkBot.Tests;

public class GitHubPullRequestCommenterGuardTests
{
    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new GitHubPullRequestCommenter(null!, "token"));

        Assert.Equal("httpClient", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyToken_ThrowsArgumentException(string? token)
    {
        using var httpClient = new HttpClient();

        var ex = Assert.ThrowsAny<ArgumentException>(() => new GitHubPullRequestCommenter(httpClient, token!));

        Assert.Equal("token", ex.ParamName);
    }

    [Fact]
    public void PullRequestRef_OwnerContainsSlash_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PullRequestRef("a/b", "r", 1));

        Assert.Equal("owner", ex.ParamName);
    }

    [Fact]
    public void PullRequestRef_RepositoryContainsSlash_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PullRequestRef("a", "r/x", 1));

        Assert.Equal("repository", ex.ParamName);
    }

    [Fact]
    public void PullRequestRef_NumberZero_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PullRequestRef("a", "r", 0));

        Assert.Equal("number", ex.ParamName);
    }

    [Fact]
    public void UpsertCommentAsync_EmptyBody_ThrowsSynchronously()
    {
        using var httpClient = new HttpClient();
        var commenter = new GitHubPullRequestCommenter(httpClient, "token");
        var pullRequest = new PullRequestRef("owner", "repo", 1);

        // Must throw from the call itself, not return a faulted Task.
        var ex = Assert.Throws<ArgumentException>(() => { _ = commenter.UpsertCommentAsync(pullRequest, "", "<!-- marker -->"); });

        Assert.Equal("body", ex.ParamName);
    }
}
