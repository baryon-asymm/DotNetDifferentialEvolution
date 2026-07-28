namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// Maps a probability in <c>[0, 1]</c> onto the 64-bit domain the raw generator draws in, so a
/// Bernoulli trial can be decided by an integer comparison instead of a floating-point one.
/// </summary>
/// <remarks>
/// Both sides of a comparison must be scaled by this same function for the integer test to mean
/// what the floating-point test meant: because it is monotone, <c>u &lt;= p</c> and
/// <c>Scale(u) &lt;= Scale(p)</c> agree except where the two arguments fall in the same 2^-64
/// bucket — a disagreement of the order of the rounding error of the original comparison.
/// </remarks>
internal static class RandomThreshold
{
    /// <summary>2^64 — one past the largest value <see cref="ulong"/> can hold.</summary>
    private const double Domain = 18446744073709551616.0;

    /// <summary>
    /// Scales <paramref name="value"/> from <c>[0, 1]</c> onto <c>[0, ulong.MaxValue]</c>.
    /// </summary>
    /// <param name="value">A probability, or a draw from the unit interval.</param>
    /// <returns>The corresponding 64-bit threshold.</returns>
    /// <remarks>
    /// The upper guard is not redundant. A probability of exactly 1.0 — which every
    /// constant-parameter strategy is free to be configured with — scales to exactly 2^64, one
    /// past what a <see cref="ulong"/> holds, and an unchecked conversion of an out-of-range
    /// <see cref="double"/> yields an unspecified value. Everything strictly below 1.0 converts
    /// exactly and needs no clamping.
    /// </remarks>
    public static ulong Scale(
        double value)
    {
        var scaled = value * Domain;

        if (scaled <= 0.0)
            return 0UL;

        return scaled >= Domain ? ulong.MaxValue : (ulong)scaled;
    }
}
