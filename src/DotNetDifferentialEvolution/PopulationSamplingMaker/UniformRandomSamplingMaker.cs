using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.PopulationSamplingMaker;

/// <summary>
/// Represents a uniform random sampling maker for population initialization.
/// </summary>
public class UniformRandomSamplingMaker : IPopulationSamplingMaker
{
    private readonly ReadOnlyMemory<double> _lowerBound;
    private readonly ReadOnlyMemory<double> _upperBound;

    private BaseRandomProvider _randomProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UniformRandomSamplingMaker"/> class.
    /// </summary>
    /// <param name="lowerBound">The lower bound of the genes.</param>
    /// <param name="upperBound">The upper bound of the genes.</param>
    public UniformRandomSamplingMaker(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound)
        : this(lowerBound, upperBound, new RandomProvider())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniformRandomSamplingMaker"/> class drawing
    /// from the given random source.
    /// </summary>
    /// <param name="lowerBound">The lower bound of the genes.</param>
    /// <param name="upperBound">The upper bound of the genes.</param>
    /// <param name="randomProvider">The random source to sample from.</param>
    public UniformRandomSamplingMaker(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound,
        BaseRandomProvider randomProvider)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        _lowerBound = lowerBound;
        _upperBound = upperBound;
        _randomProvider = randomProvider;
    }

    /// <inheritdoc />
    public void UseRandomProvider(
        BaseRandomProvider randomProvider)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        _randomProvider = randomProvider;
    }

    /// <summary>
    /// Samples the population with uniform random values within the specified bounds.
    /// </summary>
    /// <param name="population">The population to be sampled.</param>
    public void SamplePopulation(
        Span<double> population)
    {
        var genomeSize = _lowerBound.Length;

        var random = _randomProvider;
        for (var i = 0; i < population.Length; i++)
        {
            var geneIndex = i % genomeSize;
            var lower = _lowerBound.Span[geneIndex];
            var upper = _upperBound.Span[geneIndex];
            var draw = random.NextDouble();
            var width = upper - lower;
            // A finite box can be wider than double.MaxValue. Its endpoints then have opposite signs.
            var sample = double.IsPositiveInfinity(width) && double.IsFinite(lower) && double.IsFinite(upper)
                ? (1.0 - draw) * lower + draw * upper
                : draw * width + lower;
            population[i] = sample > upper && lower <= upper ? upper : sample;
        }
    }
}
