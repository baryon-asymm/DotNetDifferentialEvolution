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
    /// <remarks>
    /// An individual the objective scored <see cref="double.NaN"/> is ranked last. .NET's default
    /// <see cref="double"/> comparer implements a total order in which NaN sorts <em>first</em>, so
    /// a plain sort would rank such an individual as the best — putting it in every p-best pool and
    /// making L-SHADE's population reduction preferentially retain it. NaN is substituted with
    /// <see cref="double.PositiveInfinity"/> in the sort keys, matching the engine-wide rule that
    /// NaN is worse than every real value.
    /// </remarks>
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
            var ffValue = populationFfValues[i];
            keys[i] = double.IsNaN(ffValue) ? double.PositiveInfinity : ffValue;
            indices[i] = i;
        }

        keys.Sort(indices);
    }
}
