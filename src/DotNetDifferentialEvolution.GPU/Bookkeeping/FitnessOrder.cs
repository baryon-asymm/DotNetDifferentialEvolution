namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The order of the fitness ranking: by key, <see cref="double.NaN"/> read as +∞ as the CPU package's
/// <c>PopulationSortHelper</c> reads it, and among equal keys by index. One total order, so every way of ranking gives
/// the same ranking (ACCEPTANCE.md, S9). The CPU package's sort leaves equal keys in no particular order; this is one of
/// its orders. The ranking kernels compare the integer <see cref="OrderKey"/> and never a double: a double comparison
/// runs on the device's double-precision unit, which a consumer GPU has at a small fraction of its integer rate
/// (ACCEPTANCE.md, A3).
/// </summary>
internal static class FitnessOrder
{
    /// <summary>The order key of +∞, and so of <see cref="double.NaN"/>: the bits of +∞.</summary>
    public const long InfinityKey = 0x7FF0000000000000L;

    /// <summary>The sort key of a fitness value: the value, or +∞ for <see cref="double.NaN"/>.</summary>
    /// <param name="fitness">The fitness value.</param>
    /// <returns>The key.</returns>
    public static double KeyOf(double fitness) => double.IsNaN(fitness) ? double.PositiveInfinity : fitness;

    /// <summary>
    /// The order of <see cref="KeyOf"/> as an integer, computed on the value's bits with no floating-point operation. A
    /// non-negative value maps to its bits, a negative one to the negation of its magnitude's bits, so that a larger
    /// magnitude gives a lower key; −0 maps to the key of +0, and every <see cref="double.NaN"/>, whatever its sign or
    /// payload, to the key of +∞.
    /// </summary>
    /// <param name="fitness">The fitness value.</param>
    /// <returns>A key that orders as <see cref="KeyOf"/> does, equal exactly where the keys are equal.</returns>
    public static long OrderKey(double fitness)
    {
        var bits = BitConverter.DoubleToInt64Bits(fitness);
        var magnitude = bits & long.MaxValue;
        var key = bits < 0 ? -magnitude : bits;
        return magnitude > InfinityKey ? InfinityKey : key;
    }

    /// <summary>Whether entry A comes before entry B: a lower key, or an equal key and a lower index.</summary>
    /// <param name="keyA">A's order key.</param>
    /// <param name="indexA">A's index.</param>
    /// <param name="keyB">B's order key.</param>
    /// <param name="indexB">B's index.</param>
    /// <returns>Whether A precedes B.</returns>
    public static bool Precedes(long keyA, int indexA, long keyB, int indexB) =>
        keyA < keyB || (keyA == keyB && indexA < indexB);
}
