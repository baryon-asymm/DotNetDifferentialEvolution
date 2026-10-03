using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.Tests.Common.Helpers;

/// <summary>
/// Owns the four buffers a <see cref="DotNetDifferentialEvolution.Models.ProblemContext"/> works on — the
/// population, the trial population and the fitness values of each — as slices of one array,
/// and fills the population for a test.
/// </summary>
public class PopulationHelper
{
    private readonly int _genomeSize;

    private readonly Memory<double> _memory;
    private readonly int _populationSize;

    /// <summary>
    /// Initializes a new instance and allocates zero-filled buffers for the given sizes.
    /// </summary>
    /// <param name="populationSize">The number of individuals.</param>
    /// <param name="genomeSize">The number of genes per individual.</param>
    public PopulationHelper(
        int populationSize,
        int genomeSize)
    {
        _populationSize = populationSize;
        _genomeSize = genomeSize;

        var memorySize = 2 * _populationSize * _genomeSize + 2 * _populationSize;
        _memory = new double[memorySize];
    }

    /// <summary>Gets the population genes, individual after individual (<c>populationSize × genomeSize</c>).</summary>
    public Memory<double> Population => _memory[..(_populationSize * _genomeSize)];

    /// <summary>Gets the trial population genes, laid out like <see cref="Population"/>.</summary>
    public Memory<double> TrialPopulation =>
        _memory[(_populationSize * _genomeSize)..(2 * _populationSize * _genomeSize)];

    /// <summary>Gets the fitness values of the population, one per individual.</summary>
    public Memory<double> PopulationFfValues =>
        _memory[(2 * _populationSize * _genomeSize)..(2 * _populationSize * _genomeSize + _populationSize)];

    /// <summary>Gets the fitness values of the trial population, one per individual.</summary>
    public Memory<double> TrialPopulationFfValues => _memory[(2 * _populationSize * _genomeSize + _populationSize)..];

    /// <summary>
    /// Fills <see cref="Population"/> with genes drawn uniformly between the bounds of each gene.
    /// </summary>
    /// <param name="lowerBounds">The lower bound of each gene; <c>genomeSize</c> long.</param>
    /// <param name="upperBounds">The upper bound of each gene; <c>genomeSize</c> long.</param>
    /// <param name="random">
    /// The generator to draw from; a <see cref="SeededRandomProvider"/> makes the population
    /// reproducible. Defaults to a new unseeded <see cref="RandomProvider"/>.
    /// </param>
    public void InitializePopulationWithRandomValues(
        ReadOnlySpan<double> lowerBounds,
        ReadOnlySpan<double> upperBounds,
        BaseRandomProvider? random = null)
    {
        random ??= new RandomProvider();
        for (var i = 0; i < _populationSize * _genomeSize; i++)
        {
            var j = i % _genomeSize;
            _memory.Span[i] = lowerBounds[j] + random.NextDouble() * (upperBounds[j] - lowerBounds[j]);
        }
    }

    /// <summary>
    /// Evaluates every individual of <see cref="Population"/> and stores the result in
    /// <see cref="PopulationFfValues"/>.
    /// </summary>
    /// <param name="evaluator">The fitness function to evaluate the individuals with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="evaluator"/> is <see langword="null"/>.</exception>
    public void EvaluatePopulationFfValues(IFitnessFunctionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        var populationFfValues = PopulationFfValues.Span;
        var population = Population.Span;

        for (var i = 0; i < _populationSize; i++)
        {
            populationFfValues[i] = evaluator.Evaluate(population.Slice(i * _genomeSize, _genomeSize));
        }
    }
}
