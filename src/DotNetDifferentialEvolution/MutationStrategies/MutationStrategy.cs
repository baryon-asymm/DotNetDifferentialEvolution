using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

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

    /// <inheritdoc />
    public int MinimumPopulationSize => NumberOfIndividualsToChoose + 1;

    /// <summary>
    /// This strategy carries the F and CR it was constructed with, so it needs nothing
    /// provisioned: it builds a trial from the population and the bounds alone.
    /// </summary>
    public MutationRequirements Requirements => MutationRequirements.None;

    private readonly double _mutationForce;
    private readonly double _crossoverProbability;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationStrategy"/> class.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <param name="populationSize">The size of the population (retained for API compatibility).</param>
    /// <param name="lowerBound">The lower bound of the genes (retained for API compatibility).</param>
    /// <param name="upperBound">The upper bound of the genes (retained for API compatibility).</param>
    /// <param name="randomProvider">The random provider (retained for API compatibility; ignored).</param>
    [Obsolete("The engine supplies the random provider through MutationContext, one per worker, "
              + "so that a seeded run is reproducible and no generator is shared between threads. "
              + "Use the overload without a random provider and DifferentialEvolutionBuilder.WithSeed.")]
    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound,
        BaseRandomProvider randomProvider)
        : this(mutationForce, crossoverProbability, populationSize, lowerBound, upperBound)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationStrategy"/> class.
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
    {
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
    }

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        // Randomness comes from the context — the calling worker's own generator — even though
        // F and CR do not. Holding a generator here would put every worker on one shared stream.
        var random = context.RandomProvider;

        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];
        RandomIndexSelector.FillDistinctIndices(
            indexes, context.PopulationSize, context.IndividualIndex, random);

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
            random);
    }
}
