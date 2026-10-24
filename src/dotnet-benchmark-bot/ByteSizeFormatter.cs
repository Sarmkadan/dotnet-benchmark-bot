using System.Globalization;

namespace DotnetBenchmarkBot;

/// <summary>
/// Formats byte counts and allocation deltas for human-readable benchmark reports.
/// </summary>
public static class ByteSizeFormatter
{
    private const double Step = 1024;

    private static readonly string[] Units = ["B", "KB", "MB", "GB"];

    /// <summary>
    /// Formats a byte count using binary units (1024 steps) up to <c>GB</c>.
    /// </summary>
    /// <param name="bytes">Byte count, or <see langword="null"/> when unavailable. Negative values are formatted with a leading <c>-</c>.</param>
    /// <returns>
    /// <c>-</c> for <see langword="null"/>; an integer with <c>B</c> below 1024 bytes (e.g. <c>512 B</c>);
    /// otherwise up to two decimals with trailing zeros trimmed (e.g. <c>2 KB</c>, <c>1.5 KB</c>, <c>5 MB</c>).
    /// </returns>
    public static string FormatBytes(long? bytes) =>
        bytes is null ? "-" : FormatMagnitude(bytes.Value);

    /// <summary>
    /// Formats the change from a baseline to a candidate byte count, with an explicit sign for increases.
    /// </summary>
    /// <param name="baseline">Baseline byte count, or <see langword="null"/> when unavailable.</param>
    /// <param name="candidate">Candidate byte count, or <see langword="null"/> when unavailable.</param>
    /// <returns>
    /// <c>-</c> if either value is <see langword="null"/>; otherwise <c>candidate - baseline</c> formatted by
    /// <see cref="FormatBytes(long?)"/> with <c>+</c> for positive deltas, e.g. <c>+1.5 KB</c>, <c>-512 B</c> or <c>0 B</c>.
    /// </returns>
    public static string FormatDelta(long? baseline, long? candidate)
    {
        if (baseline is null || candidate is null)
        {
            return "-";
        }

        // Subtract in floating point so extreme values cannot overflow a long.
        var delta = (double)candidate.Value - baseline.Value;
        var sign = delta > 0 ? "+" : string.Empty;
        return $"{sign}{FormatMagnitude(delta)}";
    }

    private static string FormatMagnitude(double bytes)
    {
        var sign = bytes < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(bytes);

        if (magnitude < Step)
        {
            return $"{sign}{magnitude.ToString("0", CultureInfo.InvariantCulture)} B";
        }

        var index = 1;
        var value = RoundToHundredths(magnitude / Step);

        // Rounding can push the value to 1024 (e.g. 1048575 bytes -> 1024.00 KB); move to the next unit in that case.
        while (value >= Step && index < Units.Length - 1)
        {
            index++;
            value = RoundToHundredths(magnitude / Math.Pow(Step, index));
        }

        return $"{sign}{value.ToString("0.##", CultureInfo.InvariantCulture)} {Units[index]}";
    }

    private static double RoundToHundredths(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
