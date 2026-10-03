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
/// <param name="mutationForce">The mutation force F, used for every individual.</param>
/// <param name="crossoverProbability">The crossover probability CR, used for every individual.</param>
public class MutationStrategy(
    double mutationForce,
    double crossoverProbability) : IMutationStrategy
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

    private readonly double _mutationForce = mutationForce;
    private readonly double _crossoverProbability = crossoverProbability;

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        // Randomness comes from the context — the calling worker's own generator — even though
        // F and CR do not. Holding a generator here would put every worker on one shared stream.
        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];
        RandomIndexSelector.FillDistinctIndices(indexes, in context);

        var genomeSize = context.GenomeSize;
        var population = context.Population;
        var firstIndividual = population.Slice(indexes[0] * genomeSize, genomeSize);
        var secondIndividual = population.Slice(indexes[1] * genomeSize, genomeSize);
        var thirdIndividual = population.Slice(indexes[2] * genomeSize, genomeSize);

        MutationMath.AssignBasePlusScaledDifference(
            context.TrialIndividual, firstIndividual, secondIndividual, thirdIndividual, _mutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(in context, _crossoverProbability);
    }
}
