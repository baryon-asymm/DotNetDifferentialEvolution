using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// The <c>DE/rand/2/bin</c> mutation strategy:
/// <c>v = x_r1 + F * (x_r2 - x_r3) + F * (x_r4 - x_r5)</c> followed by binomial crossover.
/// Two difference vectors give greater diversity (more exploration) than rand/1.
/// Uses the per-individual F and CR supplied by the <see cref="MutationContext"/>.
/// </summary>
public class RandTwoMutationStrategy : IMutationStrategy
{
    private const int NumberOfIndividualsToChoose = 5;

    /// <inheritdoc />
    public int MinimumPopulationSize => NumberOfIndividualsToChoose + 1;

    /// <inheritdoc />
    public MutationRequirements Requirements => MutationRequirements.ControlParameters;

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        var random = context.RandomProvider;
        var genomeSize = context.GenomeSize;
        var population = context.Population;
        var mutationForce = context.MutationForce;

        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];
        RandomIndexSelector.FillDistinctIndices(
            indexes, context.PopulationSize, context.IndividualIndex, random);

        var baseIndividual = population.Slice(indexes[0] * genomeSize, genomeSize);
        var first = population.Slice(indexes[1] * genomeSize, genomeSize);
        var second = population.Slice(indexes[2] * genomeSize, genomeSize);
        var third = population.Slice(indexes[3] * genomeSize, genomeSize);
        var fourth = population.Slice(indexes[4] * genomeSize, genomeSize);

        MutationMath.AssignBasePlusScaledDifference(
            context.TrialIndividual, baseIndividual, first, second, mutationForce);
        MutationMath.AddScaledDifference(context.TrialIndividual, third, fourth, mutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(
            context.IndividualIndex, context.CrossoverProbability, population,
            context.TrialIndividual, context.LowerBound, context.UpperBound, random);
    }
}
