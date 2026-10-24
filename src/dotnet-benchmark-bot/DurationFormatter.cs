using System.Globalization;

namespace DotnetBenchmarkBot;

/// <summary>
/// Formats durations and ratios for human-readable benchmark reports.
/// </summary>
public static class DurationFormatter
{
    private static readonly (double Divisor, string Unit)[] DurationUnits =
    [
        (1, "ns"),
        (1_000, "us"),
        (1_000_000, "ms"),
        (1_000_000_000, "s"),
    ];

    /// <summary>
    /// Formats a duration in nanoseconds using the largest unit that keeps the value at or above 1
    /// (<c>ns</c>, <c>us</c>, <c>ms</c> or <c>s</c>), with up to three significant digits.
    /// </summary>
    /// <param name="nanoseconds">Duration in nanoseconds. Negative values are formatted with a leading <c>-</c>.</param>
    /// <returns>
    /// The formatted duration, e.g. <c>250 ns</c>, <c>1.235 us</c> or <c>45.6 ms</c>;
    /// <c>n/a</c> for NaN; <c>inf</c> or <c>-inf</c> for infinities.
    /// </returns>
    public static string FormatNanoseconds(double nanoseconds)
    {
        if (double.IsNaN(nanoseconds))
        {
            return "n/a";
        }

        if (double.IsInfinity(nanoseconds))
        {
            return nanoseconds > 0 ? "inf" : "-inf";
        }

        var sign = nanoseconds < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(nanoseconds);

        var index = 0;
        while (index < DurationUnits.Length - 1 && magnitude >= DurationUnits[index + 1].Divisor)
        {
            index++;
        }

        var value = RoundToSignificant(magnitude / DurationUnits[index].Divisor);

        // Rounding can push the value to 1000 (e.g. 999.9996 ns); move to the next unit in that case.
        if (value >= 1_000 && index < DurationUnits.Length - 1)
        {
            index++;
            value = RoundToSignificant(magnitude / DurationUnits[index].Divisor);
        }

        return $"{sign}{value.ToString(SignificantPattern(value), CultureInfo.InvariantCulture)} {DurationUnits[index].Unit}";
    }

    /// <summary>
    /// Formats a ratio as a signed percentage change relative to 1.0 (for example, 1.125 becomes <c>+12.5%</c>).
    /// </summary>
    /// <param name="ratio">Candidate divided by baseline. Values above 1.0 are increases, below 1.0 are decreases.</param>
    /// <returns>
    /// The signed percentage with one decimal place, e.g. <c>+12.5%</c>, <c>-10.0%</c> or <c>0.0%</c>;
    /// <c>n/a</c> for NaN; <c>+inf</c> or <c>-inf</c> for infinities.
    /// </returns>
    public static string FormatRatio(double ratio)
    {
        if (double.IsNaN(ratio))
        {
            return "n/a";
        }

        if (double.IsInfinity(ratio))
        {
            return ratio > 0 ? "+inf" : "-inf";
        }

        var percent = Math.Round((ratio - 1.0) * 100.0, 1, MidpointRounding.AwayFromZero);

        // Normalize negative zero so values like 0.9999 do not render as "-0.0%".
        if (percent == 0)
        {
            percent = 0;
        }

        var sign = percent > 0 ? "+" : string.Empty;
        return $"{sign}{percent.ToString("0.0", CultureInfo.InvariantCulture)}%";
    }

    private static double RoundToSignificant(double value) =>
        Math.Round(value, DecimalsFor(value), MidpointRounding.AwayFromZero);

    private static int DecimalsFor(double value) => value < 10 ? 3 : value < 100 ? 2 : 0;

    private static string SignificantPattern(double value) => DecimalsFor(value) switch
    {
        3 => "0.###",
        2 => "0.##",
        _ => "0",
    };
}
