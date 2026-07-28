using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// The <c>DE/current-to-best/1/bin</c> mutation strategy:
/// <c>v = x_i + F * (x_best - x_i) + F * (x_r1 - x_r2)</c> followed by binomial crossover.
/// Uses the per-individual F and CR supplied by the <see cref="MutationContext"/>.
/// </summary>
public class CurrentToBestMutationStrategy : IMutationStrategy
{
    private const int NumberOfDifferenceIndividuals = 2;

    /// <inheritdoc />
    public int MinimumPopulationSize => NumberOfDifferenceIndividuals + 1;

    /// <inheritdoc />
    public MutationRequirements Requirements =>
        MutationRequirements.ControlParameters | MutationRequirements.BestIndividual;

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        var random = context.RandomProvider;
        var genomeSize = context.GenomeSize;
        var population = context.Population;
        var mutationForce = context.MutationForce;

        Span<int> indexes = stackalloc int[NumberOfDifferenceIndividuals];
        RandomIndexSelector.FillDistinctIndices(
            indexes, context.PopulationSize, context.IndividualIndex, random);

        var current = population.Slice(context.IndividualIndex * genomeSize, genomeSize);
        var bestIndividual = population.Slice(context.BestIndividualIndex * genomeSize, genomeSize);
        var first = population.Slice(indexes[0] * genomeSize, genomeSize);
        var second = population.Slice(indexes[1] * genomeSize, genomeSize);

        // v = x_i + F * (x_best - x_i)
        MutationMath.AssignCurrentToTarget(context.TrialIndividual, current, bestIndividual, mutationForce);
        // v += F * (x_r1 - x_r2)
        MutationMath.AddScaledDifference(context.TrialIndividual, first, second, mutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(
            context.IndividualIndex, context.CrossoverProbability, population,
            context.TrialIndividual, context.LowerBound, context.UpperBound, random);
    }
}
