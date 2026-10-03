namespace DotNetDifferentialEvolution.GPU;

/// <summary>The index of the best individual: lowest fitness, <see cref="double.NaN"/> worst, a tie to the lowest index.</summary>
internal static class BestPick
{
    /// <summary>The best index of <paramref name="fitness"/>; 0 when every value is <see cref="double.NaN"/>.</summary>
    /// <param name="fitness">The fitness values; not empty.</param>
    /// <returns>The index.</returns>
    public static int IndexOf(ReadOnlySpan<double> fitness)
    {
        var best = 0;
        for (var i = 1; i < fitness.Length; i++)
        {
            if (fitness[i] < fitness[best] || (double.IsNaN(fitness[best]) && !double.IsNaN(fitness[i])))
            {
                best = i;
            }
        }

        return best;
    }
}
