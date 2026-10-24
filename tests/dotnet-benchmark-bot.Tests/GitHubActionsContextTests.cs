namespace DotnetBenchmarkBot.Tests;

public class GitHubActionsContextTests
{
    [Fact]
    public void FromVariables_InsideActions_ReadsFlagAndToken()
    {
        var context = GitHubActionsContext.FromVariables(Lookup(
            ("GITHUB_ACTIONS", "true"),
            ("GITHUB_TOKEN", "token-value")));

        Assert.True(context.IsGitHubActions);
        Assert.Equal("token-value", context.Token);
        Assert.Null(context.PullRequest);
    }

    [Fact]
    public void FromVariables_OutsideActions_FlagIsFalse()
    {
        var context = GitHubActionsContext.FromVariables(Lookup(("GITHUB_ACTIONS", "false")));

        Assert.False(context.IsGitHubActions);
    }

    [Fact]
    public void FromVariables_EmptyToken_IsNull()
    {
        var context = GitHubActionsContext.FromVariables(Lookup(("GITHUB_TOKEN", "")));

        Assert.Null(context.Token);
    }

    [Fact]
    public void FromVariables_PullRequestRef_ResolvesNumberFromRef()
    {
        var context = GitHubActionsContext.FromVariables(Lookup(
            ("GITHUB_REPOSITORY", "Sarmkadan/dotnet-benchmark-bot"),
            ("GITHUB_REF", "refs/pull/42/merge")));

        Assert.Equal(new PullRequestRef("Sarmkadan", "dotnet-benchmark-bot", 42), context.PullRequest);
    }

    [Fact]
    public void FromVariables_RefWithoutPullRequest_FallsBackToEventPayload()
    {
        var eventPath = WriteEvent("""{ "pull_request": { "number": 7 } }""");
        try
        {
            var context = GitHubActionsContext.FromVariables(Lookup(
                ("GITHUB_REPOSITORY", "owner/repo"),
                ("GITHUB_REF", "refs/heads/main"),
                ("GITHUB_EVENT_PATH", eventPath)));

            Assert.Equal(new PullRequestRef("owner", "repo", 7), context.PullRequest);
        }
        finally
        {
            File.Delete(eventPath);
        }
    }

    [Fact]
    public void FromVariables_RefTakesPrecedenceOverEventPayload()
    {
        var eventPath = WriteEvent("""{ "number": 99 }""");
        try
        {
            var context = GitHubActionsContext.FromVariables(Lookup(
                ("GITHUB_REPOSITORY", "owner/repo"),
                ("GITHUB_REF", "refs/pull/5/merge"),
                ("GITHUB_EVENT_PATH", eventPath)));

            Assert.Equal(5, context.PullRequest?.Number);
        }
        finally
        {
            File.Delete(eventPath);
        }
    }

    [Fact]
    public void FromVariables_TopLevelNumberInEvent_IsUsed()
    {
        var eventPath = WriteEvent("""{ "action": "opened", "number": 13 }""");
        try
        {
            var context = GitHubActionsContext.FromVariables(Lookup(
                ("GITHUB_REPOSITORY", "owner/repo"),
                ("GITHUB_EVENT_PATH", eventPath)));

            Assert.Equal(13, context.PullRequest?.Number);
        }
        finally
        {
            File.Delete(eventPath);
        }
    }

    [Fact]
    public void FromVariables_MalformedEventPayload_YieldsNoPullRequest()
    {
        var eventPath = WriteEvent("{ not json");
        try
        {
            var context = GitHubActionsContext.FromVariables(Lookup(
                ("GITHUB_REPOSITORY", "owner/repo"),
                ("GITHUB_EVENT_PATH", eventPath)));

            Assert.Null(context.PullRequest);
        }
        finally
        {
            File.Delete(eventPath);
        }
    }

    [Fact]
    public void FromVariables_MissingEventFile_YieldsNoPullRequest()
    {
        var context = GitHubActionsContext.FromVariables(Lookup(
            ("GITHUB_REPOSITORY", "owner/repo"),
            ("GITHUB_EVENT_PATH", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"))));

        Assert.Null(context.PullRequest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("noslash")]
    [InlineData("/repo")]
    [InlineData("owner/")]
    [InlineData("owner/a/b")]
    public void FromVariables_InvalidRepository_YieldsNoPullRequest(string? repository)
    {
        var context = GitHubActionsContext.FromVariables(Lookup(
            ("GITHUB_REPOSITORY", repository),
            ("GITHUB_REF", "refs/pull/1/merge")));

        Assert.Null(context.PullRequest);
    }

    [Fact]
    public void FromVariables_MissingNumber_YieldsNoPullRequest()
    {
        var context = GitHubActionsContext.FromVariables(Lookup(
            ("GITHUB_REPOSITORY", "owner/repo"),
            ("GITHUB_REF", "refs/heads/main")));

        Assert.Null(context.PullRequest);
    }

    [Fact]
    public void FromVariables_NullGetter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GitHubActionsContext.FromVariables(null!));
    }

    [Theory]
    [InlineData("Sarmkadan/dotnet-benchmark-bot#42", "Sarmkadan", "dotnet-benchmark-bot", 42)]
    [InlineData("owner/repo#1", "owner", "repo", 1)]
    public void TryParsePullRequestRef_ValidValue_ReturnsReference(string value, string owner, string repository, int number)
    {
        var parsed = GitHubActionsContext.TryParsePullRequestRef(value, out var result);

        Assert.True(parsed);
        Assert.Equal(new PullRequestRef(owner, repository, number), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("owner/repo")]
    [InlineData("owner/repo#")]
    [InlineData("owner/repo#abc")]
    [InlineData("owner/repo#0")]
    [InlineData("owner/repo#-3")]
    [InlineData("owner/repo#1#2")]
    [InlineData("/repo#1")]
    [InlineData("owner/#1")]
    [InlineData("owner/a/b#1")]
    [InlineData("own er/repo#1")]
    [InlineData("#1")]
    public void TryParsePullRequestRef_InvalidValue_ReturnsFalse(string? value)
    {
        var parsed = GitHubActionsContext.TryParsePullRequestRef(value!, out var result);

        Assert.False(parsed);
        Assert.Null(result);
    }

    private static Func<string, string?> Lookup(params (string Name, string? Value)[] variables)
    {
        var map = variables.ToDictionary(pair => pair.Name, pair => pair.Value);
        return name => map.TryGetValue(name, out var value) ? value : null;
    }

    private static string WriteEvent(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
