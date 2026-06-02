using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// The <c>DE/rand/1/bin</c> mutation strategy that takes its per-individual F and CR from
/// the <see cref="MutationContext"/> (as opposed to <see cref="MutationStrategy"/>, which
/// uses fixed constructor parameters). Used by the self-adaptive variants (jDE).
/// </summary>
public class RandMutationStrategy : IMutationStrategy
{
    private const int NumberOfIndividualsToChoose = 3;

    /// <inheritdoc />
    public int MinimumPopulationSize => NumberOfIndividualsToChoose + 1;

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        var random = context.RandomProvider;
        var genomeSize = context.GenomeSize;
        var population = context.Population;

        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];
        RandomIndexSelector.FillDistinctIndices(
            indexes, context.PopulationSize, context.IndividualIndex, random);

        var first = population.Slice(indexes[0] * genomeSize, genomeSize);
        var second = population.Slice(indexes[1] * genomeSize, genomeSize);
        var third = population.Slice(indexes[2] * genomeSize, genomeSize);

        MutationMath.AssignBasePlusScaledDifference(
            context.TrialIndividual, first, second, third, context.MutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(
            context.IndividualIndex, context.CrossoverProbability, population,
            context.TrialIndividual, context.LowerBound, context.UpperBound, random);
    }
}
