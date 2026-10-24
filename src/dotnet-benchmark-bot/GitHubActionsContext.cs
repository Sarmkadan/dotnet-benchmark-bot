using System.Globalization;
using System.Text.Json;

namespace DotnetBenchmarkBot;

/// <summary>
/// Discovers the pull request that a GitHub Actions run is building from the runner's environment variables.
/// </summary>
/// <remarks>
/// Reads <c>GITHUB_ACTIONS</c>, <c>GITHUB_TOKEN</c>, <c>GITHUB_REPOSITORY</c>, <c>GITHUB_REF</c> and <c>GITHUB_EVENT_PATH</c>.
/// The pull request number comes from <c>GITHUB_REF</c> when it has the form <c>refs/pull/{number}/merge</c>;
/// otherwise it comes from the event payload at <c>GITHUB_EVENT_PATH</c>.
/// </remarks>
public sealed class GitHubActionsContext
{
    private GitHubActionsContext(bool isGitHubActions, string? token, PullRequestRef? pullRequest)
    {
        IsGitHubActions = isGitHubActions;
        Token = token;
        PullRequest = pullRequest;
    }

    /// <summary>
    /// Gets the token from <c>GITHUB_TOKEN</c>, or <see langword="null"/> when the variable is unset or empty.
    /// </summary>
    public string? Token { get; }

    /// <summary>
    /// Gets the pull request being built, or <see langword="null"/> unless the owner, repository and number are all resolved.
    /// </summary>
    public PullRequestRef? PullRequest { get; }

    /// <summary>
    /// Gets a value indicating whether the process runs inside GitHub Actions, i.e. <c>GITHUB_ACTIONS</c> is <c>true</c>.
    /// </summary>
    public bool IsGitHubActions { get; }

    /// <summary>
    /// Creates a context from the current process environment variables.
    /// </summary>
    /// <returns>A context describing the current GitHub Actions run, or one with no pull request when run outside Actions.</returns>
    public static GitHubActionsContext FromEnvironment() =>
        FromVariables(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Creates a context from variables supplied by <paramref name="getVariable"/>, so discovery can be tested without touching the process environment.
    /// </summary>
    /// <param name="getVariable">Returns the value of a variable by name, or <see langword="null"/> when it is unset.</param>
    /// <returns>
    /// A context whose <see cref="PullRequest"/> is set only when <c>GITHUB_REPOSITORY</c> names an owner and repository
    /// and a positive pull request number is found in <c>GITHUB_REF</c> or, failing that, in the event payload.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="getVariable"/> is <see langword="null"/>.</exception>
    public static GitHubActionsContext FromVariables(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        var isGitHubActions = getVariable("GITHUB_ACTIONS") == "true";
        var token = getVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            token = null;
        }

        PullRequestRef? pullRequest = null;
        if (TryParseOwnerAndRepository(getVariable("GITHUB_REPOSITORY"), out var owner, out var repository))
        {
            var number = TryParsePullRequestNumberFromRef(getVariable("GITHUB_REF"))
                ?? TryReadPullRequestNumberFromEvent(getVariable("GITHUB_EVENT_PATH"));

            if (number is int resolved)
            {
                pullRequest = new PullRequestRef(owner, repository, resolved);
            }
        }

        return new GitHubActionsContext(isGitHubActions, token, pullRequest);
    }

    /// <summary>
    /// Parses a pull request reference in the form <c>owner/repo#42</c>.
    /// </summary>
    /// <param name="value">The text to parse, for example <c>Sarmkadan/dotnet-benchmark-bot#42</c>.</param>
    /// <param name="result">
    /// When this method returns <see langword="true"/>, the parsed reference; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> has exactly one <c>/</c> separating non-empty owner and repository
    /// names without whitespace, followed by <c>#</c> and a positive number; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParsePullRequestRef(string value, out PullRequestRef? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hashIndex = value.IndexOf('#');
        if (hashIndex < 0)
        {
            return false;
        }

        if (!TryParseOwnerAndRepository(value[..hashIndex], out var owner, out var repository))
        {
            return false;
        }

        if (!int.TryParse(value[(hashIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            return false;
        }

        result = new PullRequestRef(owner, repository, number);
        return true;
    }

    private static bool TryParseOwnerAndRepository(string? value, out string owner, out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var slashIndex = value.IndexOf('/');
        if (slashIndex <= 0)
        {
            return false;
        }

        var candidateRepository = value[(slashIndex + 1)..];
        if (candidateRepository.Length == 0 || candidateRepository.Contains('/') || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        owner = value[..slashIndex];
        repository = candidateRepository;
        return true;
    }

    private static int? TryParsePullRequestNumberFromRef(string? gitRef)
    {
        const string Prefix = "refs/pull/";
        const string Suffix = "/merge";

        if (gitRef is null
            || gitRef.Length < Prefix.Length + Suffix.Length
            || !gitRef.StartsWith(Prefix, StringComparison.Ordinal)
            || !gitRef.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var numberText = gitRef[Prefix.Length..^Suffix.Length];
        return int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : null;
    }

    private static int? TryReadPullRequestNumberFromEvent(string? eventPath)
    {
        if (string.IsNullOrEmpty(eventPath) || !File.Exists(eventPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(eventPath));
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("pull_request", out var pullRequest)
                && pullRequest.ValueKind == JsonValueKind.Object
                && TryGetPositiveInt32(pullRequest, "number", out var nested))
            {
                return nested;
            }

            return TryGetPositiveInt32(root, "number", out var topLevel) ? topLevel : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable or malformed event payload means the number is unknown, not that discovery should fail.
            return null;
        }
    }

    private static bool TryGetPositiveInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;

        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value)
            && value > 0;
    }
}
