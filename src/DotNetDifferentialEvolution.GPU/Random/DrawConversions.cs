namespace DotNetDifferentialEvolution.GPU.Random;

/// <summary>
/// The pure functions that turn 32-bit random words into the values the DE step consumes.
/// Shared by every draw source, so a scripted source and the Philox one convert alike.
/// </summary>
internal static class DrawConversions
{
    /// <summary>2⁻⁵³: the weight of the last bit of a 53-bit fraction.</summary>
    public const double UnitDoubleScale = 1.0 / 9007199254740992.0;

    /// <summary>
    /// A uniform index in <c>[0, n)</c> by Lemire's multiply-shift, without rejection:
    /// <c>⌊word · n / 2³²⌋</c>. The bias is below <c>n / 2³²</c>, the bound the CPU package
    /// accepts for its own index draws.
    /// </summary>
    /// <param name="word">A uniform 32-bit word.</param>
    /// <param name="n">The number of indices; at least 1.</param>
    /// <returns>An index in <c>[0, n)</c>.</returns>
    public static int ToIndex(uint word, int n) => (int)(((ulong)word * (uint)n) >> 32);

    /// <summary>
    /// A uniform double in <c>[0, 1)</c> from 53 random bits: the high 27 bits of
    /// <paramref name="high"/> and the high 26 bits of <paramref name="low"/>. Every value is a
    /// multiple of 2⁻⁵³, and the largest is <c>1 − 2⁻⁵³</c>.
    /// </summary>
    /// <param name="high">The word giving the top 27 bits.</param>
    /// <param name="low">The word giving the next 26 bits.</param>
    /// <returns>A double in <c>[0, 1)</c>.</returns>
    public static double ToUnitDouble(uint high, uint low) =>
        (((ulong)(high >> 5) << 26) | (low >> 6)) * UnitDoubleScale;

    /// <summary>A uniform 64-bit word from two 32-bit words, <paramref name="high"/> first.</summary>
    /// <param name="high">The upper 32 bits.</param>
    /// <param name="low">The lower 32 bits.</param>
    /// <returns>The 64-bit word.</returns>
    public static ulong ToULong(uint high, uint low) => ((ulong)high << 32) | low;
}
