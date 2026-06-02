using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// The <c>DE/best/1/bin</c> mutation strategy:
/// <c>v = x_best + F * (x_r1 - x_r2)</c> followed by binomial crossover.
/// Uses the per-individual F and CR supplied by the <see cref="MutationContext"/>.
/// </summary>
public class BestMutationStrategy : IMutationStrategy
{
    private const int NumberOfDifferenceIndividuals = 2;

    /// <inheritdoc />
    public int MinimumPopulationSize => NumberOfDifferenceIndividuals + 1;

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        var random = context.RandomProvider;
        var genomeSize = context.GenomeSize;
        var population = context.Population;

        Span<int> indexes = stackalloc int[NumberOfDifferenceIndividuals];
        RandomIndexSelector.FillDistinctIndices(
            indexes, context.PopulationSize, context.IndividualIndex, random);

        var bestIndividual = population.Slice(context.BestIndividualIndex * genomeSize, genomeSize);
        var first = population.Slice(indexes[0] * genomeSize, genomeSize);
        var second = population.Slice(indexes[1] * genomeSize, genomeSize);

        MutationMath.AssignBasePlusScaledDifference(
            context.TrialIndividual, bestIndividual, first, second, context.MutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(
            context.IndividualIndex, context.CrossoverProbability, population,
            context.TrialIndividual, context.LowerBound, context.UpperBound, random);
    }
}
