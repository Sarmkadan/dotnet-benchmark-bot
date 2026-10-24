namespace DotnetBenchmarkBot;

/// <summary>
/// A BenchmarkDotNet benchmark name split into its type, method and parameter parts.
/// </summary>
/// <param name="TypeName">Qualified type name, including its namespace, e.g. <c>MyBenchmarks.ParserBenchmarks</c>.</param>
/// <param name="MethodName">Benchmark method name, e.g. <c>Parse</c>.</param>
/// <param name="Parameters">Text between the outer parentheses, e.g. <c>size: 1024</c>, or <see langword="null"/> when there are none.</param>
public sealed record BenchmarkName(string TypeName, string MethodName, string? Parameters);

/// <summary>
/// Parses BenchmarkDotNet <c>FullName</c> values such as <c>Namespace.Type.Method(size: 1024)</c> or <c>Namespace.Type.Method</c>.
/// </summary>
public static class BenchmarkNameParser
{
    /// <summary>
    /// Parses a benchmark full name.
    /// </summary>
    /// <param name="fullName">Benchmark full name as reported by BenchmarkDotNet.</param>
    /// <returns>The parsed name parts.</returns>
    /// <exception cref="FormatException"><paramref name="fullName"/> is not a valid benchmark name. The message contains the offending string.</exception>
    public static BenchmarkName Parse(string fullName)
    {
        if (!TryParse(fullName, out var result))
        {
            throw new FormatException($"Invalid benchmark name: '{fullName}'.");
        }

        return result!;
    }

    /// <summary>
    /// Attempts to parse a benchmark full name without throwing.
    /// </summary>
    /// <param name="fullName">Benchmark full name as reported by BenchmarkDotNet. May be <see langword="null"/>.</param>
    /// <param name="result">
    /// When this method returns <see langword="true"/>, the parsed name; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the name was parsed; <see langword="false"/> when the input is null or whitespace,
    /// the member name has no <c>.</c> separating type and method, or an opening <c>(</c> has no matching trailing <c>)</c>.
    /// </returns>
    public static bool TryParse(string fullName, out BenchmarkName? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return false;
        }

        var openIndex = fullName.IndexOf('(');
        string qualifiedMember;
        string? parameters = null;

        if (openIndex < 0)
        {
            qualifiedMember = fullName;
        }
        else
        {
            if (fullName[^1] != ')')
            {
                return false;
            }

            qualifiedMember = fullName[..openIndex];
            var inner = fullName[(openIndex + 1)..^1];
            parameters = string.IsNullOrWhiteSpace(inner) ? null : inner;
        }

        var lastDot = qualifiedMember.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == qualifiedMember.Length - 1)
        {
            return false;
        }

        result = new BenchmarkName(
            qualifiedMember[..lastDot],
            qualifiedMember[(lastDot + 1)..],
            parameters);

        return true;
    }

    /// <summary>
    /// Produces a shortened benchmark name without the namespace, suitable for narrow report table cells.
    /// </summary>
    /// <param name="fullName">Benchmark full name as reported by BenchmarkDotNet.</param>
    /// <returns>
    /// <c>Type.Method(params)</c>, or <c>Type.Method</c> when the name has no parameters. The input is returned unchanged
    /// when it cannot be parsed.
    /// </returns>
    public static string ShortName(string fullName)
    {
        if (!TryParse(fullName, out var name))
        {
            return fullName;
        }

        var typeName = name!.TypeName;
        var shortType = typeName[(typeName.LastIndexOf('.') + 1)..];

        return name.Parameters is null
            ? $"{shortType}.{name.MethodName}"
            : $"{shortType}.{name.MethodName}({name.Parameters})";
    }
}
