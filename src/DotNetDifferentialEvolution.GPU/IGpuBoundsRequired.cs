namespace DotNetDifferentialEvolution.GPU;

/// <summary>The first stage of the builder: the search box.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuBoundsRequired<TFunction>
    where TFunction : struct
{
    /// <summary>Sets the box: gene j is searched in <c>[lowerBound[j], upperBound[j]]</c>.</summary>
    /// <param name="lowerBound">The lower bound of each gene; its length is D.</param>
    /// <param name="upperBound">The upper bound of each gene; the same length.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentException">The bounds differ in length, are empty, are not finite, or a lower bound exceeds its upper bound.</exception>
    IGpuPopulationSizeRequired<TFunction> WithBounds(ReadOnlyMemory<double> lowerBound, ReadOnlyMemory<double> upperBound);
}
