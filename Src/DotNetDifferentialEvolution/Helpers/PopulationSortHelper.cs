namespace DotNetDifferentialEvolution.Helpers;

/// <summary>
/// Helpers for ranking the population by fitness, used by p-best mutation strategies.
/// </summary>
public static class PopulationSortHelper
{
    /// <summary>
    /// Fills <paramref name="sortedIndices"/> (first <paramref name="count"/> entries) with
    /// the population indices <c>0..count-1</c> ordered ascending by fitness (best first).
    /// </summary>
    /// <param name="sortedIndices">The destination index buffer.</param>
    /// <param name="populationFfValues">The fitness function values of the population.</param>
    /// <param name="count">The number of active individuals to rank.</param>
    /// <param name="keyBuffer">A scratch buffer of at least <paramref name="count"/> doubles.</param>
    public static void SortIndicesByFitness(
        Span<int> sortedIndices,
        ReadOnlySpan<double> populationFfValues,
        int count,
        Span<double> keyBuffer)
    {
        var keys = keyBuffer.Slice(0, count);
        var indices = sortedIndices.Slice(0, count);
        for (int i = 0; i < count; i++)
        {
            keys[i] = populationFfValues[i];
            indices[i] = i;
        }

        keys.Sort(indices);
    }
}
