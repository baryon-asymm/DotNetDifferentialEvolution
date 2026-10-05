namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The order of the fitness ranking: by key, <see cref="double.NaN"/> read as +∞ as the CPU package's
/// <c>PopulationSortHelper</c> reads it, and among equal keys by index. One total order, so every way of ranking gives
/// the same ranking (ACCEPTANCE.md, S9). The CPU package's sort leaves equal keys in no particular order; this is one of
/// its orders.
/// </summary>
internal static class FitnessOrder
{
    /// <summary>The sort key of a fitness value: the value, or +∞ for <see cref="double.NaN"/>.</summary>
    /// <param name="fitness">The fitness value.</param>
    /// <returns>The key.</returns>
    public static double KeyOf(double fitness) => double.IsNaN(fitness) ? double.PositiveInfinity : fitness;

    /// <summary>Whether entry A comes before entry B: a lower key, or an equal key and a lower index.</summary>
    /// <param name="keyA">A's key, never <see cref="double.NaN"/>.</param>
    /// <param name="indexA">A's index.</param>
    /// <param name="keyB">B's key, never <see cref="double.NaN"/>.</param>
    /// <param name="indexB">B's index.</param>
    /// <returns>Whether A precedes B.</returns>
    public static bool Precedes(double keyA, int indexA, double keyB, int indexB) =>
        keyA < keyB || (keyA == keyB && indexA < indexB);
}
