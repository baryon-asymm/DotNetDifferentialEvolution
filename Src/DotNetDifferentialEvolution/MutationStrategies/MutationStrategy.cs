using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// The classic <c>DE/rand/1/bin</c> mutation strategy:
/// <c>v = x_r1 + F * (x_r2 - x_r3)</c> followed by binomial crossover.
/// </summary>
/// <remarks>
/// This strategy uses the fixed mutation force and crossover probability supplied to its
/// constructor and ignores any per-individual parameters in the
/// <see cref="MutationContext"/>, preserving the original constant-parameter behavior.
/// </remarks>
public class MutationStrategy : IMutationStrategy
{
    /// <summary>
    /// The number of individuals to choose for mutation.
    /// </summary>
    public const int NumberOfIndividualsToChoose = 3;

    private readonly double _mutationForce;
    private readonly double _crossoverProbability;

    private readonly BaseRandomProvider _randomProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationStrategy"/> class.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <param name="populationSize">The size of the population (retained for API compatibility).</param>
    /// <param name="lowerBound">The lower bound of the genes (retained for API compatibility).</param>
    /// <param name="upperBound">The upper bound of the genes (retained for API compatibility).</param>
    /// <param name="randomProvider">The random provider.</param>
    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound,
        BaseRandomProvider randomProvider)
    {
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
        _randomProvider = randomProvider;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationStrategy"/> class with a default random provider.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <param name="populationSize">The size of the population (retained for API compatibility).</param>
    /// <param name="lowerBound">The lower bound of the genes (retained for API compatibility).</param>
    /// <param name="upperBound">The upper bound of the genes (retained for API compatibility).</param>
    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound)
        : this(mutationForce, crossoverProbability, populationSize, lowerBound, upperBound, new RandomProvider())
    {
    }

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];
        RandomIndexSelector.FillDistinctIndices(
            indexes, context.PopulationSize, context.IndividualIndex, _randomProvider);

        var genomeSize = context.GenomeSize;
        var population = context.Population;
        var firstIndividual = population.Slice(indexes[0] * genomeSize, genomeSize);
        var secondIndividual = population.Slice(indexes[1] * genomeSize, genomeSize);
        var thirdIndividual = population.Slice(indexes[2] * genomeSize, genomeSize);

        MutationMath.AssignBasePlusScaledDifference(
            context.TrialIndividual, firstIndividual, secondIndividual, thirdIndividual, _mutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(
            context.IndividualIndex,
            _crossoverProbability,
            population,
            context.TrialIndividual,
            context.LowerBound,
            context.UpperBound,
            _randomProvider);
    }
}
