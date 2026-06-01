using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// The <c>DE/current-to-pbest/1</c> mutation strategy used by JADE, SHADE and L-SHADE:
/// <c>v = x_i + F * (x_pbest - x_i) + F * (x_r1 - x_r2)</c>, where <c>x_pbest</c> is drawn
/// from the top <c>p%</c> of the population and <c>x_r2</c> is drawn from the union of the
/// population and the optional external archive. Reads per-individual F and CR from the
/// <see cref="MutationContext"/>.
/// </summary>
public class CurrentToPBestMutationStrategy : IMutationStrategy
{
    private readonly double _pBestRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentToPBestMutationStrategy"/> class.
    /// </summary>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    public CurrentToPBestMutationStrategy(
        double pBestRate)
    {
        if (pBestRate is <= 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(pBestRate), "p-best rate must be in (0, 1].");

        _pBestRate = pBestRate;
    }

    /// <inheritdoc />
    public void Mutate(
        in MutationContext context)
    {
        var random = context.RandomProvider;
        var genomeSize = context.GenomeSize;
        var population = context.Population;
        var populationSize = context.PopulationSize;
        var individualIndex = context.IndividualIndex;
        var mutationForce = context.MutationForce;

        // Choose x_pbest from the top p% of the population.
        var sortedIndices = context.FitnessSortedIndices;
        var topCount = Math.Clamp((int)Math.Round(_pBestRate * populationSize), 1, populationSize);
        var pBestIndex = sortedIndices.Length >= populationSize
            ? sortedIndices[random.Next(topCount)]
            : random.Next(populationSize);

        // r1 from the population, distinct from i.
        int r1;
        do { r1 = random.Next(populationSize); } while (r1 == individualIndex);

        // r2 from population ∪ archive, distinct from i and r1.
        // Indices >= populationSize address the archive; those never collide with i or r1.
        var unionSize = populationSize + context.ArchiveSize;
        int r2;
        do { r2 = random.Next(unionSize); } while (r2 == individualIndex || r2 == r1);

        var current = population.Slice(individualIndex * genomeSize, genomeSize);
        var pBest = population.Slice(pBestIndex * genomeSize, genomeSize);
        var firstDifference = population.Slice(r1 * genomeSize, genomeSize);
        var secondDifference = r2 < populationSize
            ? population.Slice(r2 * genomeSize, genomeSize)
            : context.Archive.Slice((r2 - populationSize) * genomeSize, genomeSize);

        // v = x_i + F * (x_pbest - x_i)
        MutationMath.AssignCurrentToTarget(context.TrialIndividual, current, pBest, mutationForce);
        // v += F * (x_r1 - x_r2)
        MutationMath.AddScaledDifference(context.TrialIndividual, firstDifference, secondDifference, mutationForce);

        CrossoverHelper.BinomialCrossoverAndRepair(
            individualIndex, context.CrossoverProbability, population,
            context.TrialIndividual, context.LowerBound, context.UpperBound, random);
    }
}
