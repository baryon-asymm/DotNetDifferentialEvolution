using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.MutationStrategies;

/// <summary>
/// Tests the constructor guard and the p-best pool sizing of the current-to-pbest strategy
/// used by JADE/SHADE/L-SHADE.
/// </summary>
[Trait("Category", "Unit")]
public class CurrentToPBestMutationStrategyTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_ThrowsWhenPBestRateIsOutOfRange(
        double pBestRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CurrentToPBestMutationStrategy(pBestRate));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_AcceptsRatesInTheHalfOpenInterval(
        double pBestRate)
    {
        var strategy = new CurrentToPBestMutationStrategy(pBestRate);

        Assert.NotNull(strategy);
    }

    [Theory]
    [InlineData(0.0, 0.2)]    // min must be > 0
    [InlineData(-0.1, 0.2)]   // min out of range
    [InlineData(0.05, 1.1)]   // max out of range
    public void RangeConstructor_ThrowsWhenRatesAreOutOfRange(
        double pBestRateMin,
        double pBestRateMax)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CurrentToPBestMutationStrategy(pBestRateMin, pBestRateMax));
    }

    [Fact]
    public void RangeConstructor_ThrowsWhenMinExceedsMax()
    {
        Assert.Throws<ArgumentException>(
            () => new CurrentToPBestMutationStrategy(pBestRateMin: 0.3, pBestRateMax: 0.2));
    }

    [Fact]
    public void RangeConstructor_AcceptsAValidPerIndividualRange()
    {
        var strategy = new CurrentToPBestMutationStrategy(pBestRateMin: 0.05, pBestRateMax: 0.2);

        Assert.NotNull(strategy);
    }

    [Theory]
    // p = 0.11 is the L-SHADE default. Tanabe's reference implementation sizes the pool as
    // pNP = max(round(p_best_rate * pop_size), 2) — "choose at least two best solutions" — so a
    // population whose round(p * N) falls below 2 must still offer a choice of two. A pool of
    // one would silently degrade DE/current-to-pbest/1 into DE/current-to-best/1.
    [InlineData(4, 2)]      // round(0.44) = 0 → floored to 2
    [InlineData(5, 2)]      // round(0.55) = 1 → floored to 2
    [InlineData(8, 2)]      // round(0.88) = 1 → floored to 2
    [InlineData(10, 2)]     // round(1.10) = 1 → floored to 2
    [InlineData(13, 2)]     // round(1.43) = 1 → floored to 2
    [InlineData(14, 2)]     // round(1.54) = 2 → the schedule already meets the floor
    [InlineData(20, 2)]     // round(2.20) = 2
    [InlineData(100, 11)]   // round(11.0) = 11 → large populations are untouched by the floor
    public void Mutate_NeverDrawsPBestFromAPoolSmallerThanTwo(
        int populationSize,
        int expectedPoolSize)
    {
        var poolSize = ObservePBestPoolSize(populationSize, pBestRate: 0.11);

        Assert.Equal(expectedPoolSize, poolSize);
    }

    [Fact]
    public void Mutate_AddressesThePBestPoolThroughTheFitnessRanking()
    {
        // A deliberately non-identity ranking, so the produced trial reveals which ranked slot
        // the draw addressed rather than which raw population index it happened to match.
        int[] ranking = [3, 0, 1, 2];

        // Ranked #1 is individual 3 → 0.5 * 3 - 0.5 = 1.0; ranked #2 is individual 0 → -0.5.
        Assert.Equal(1.0, RunMutation(populationSize: 4, pBestRate: 0.11, pBestDraw: 0, ranking), precision: 12);
        Assert.Equal(-0.5, RunMutation(populationSize: 4, pBestRate: 0.11, pBestDraw: 1, ranking), precision: 12);
    }

    /// <summary>
    /// Recovers the otherwise invisible p-best pool size (a local inside <c>Mutate</c>) by
    /// probing: the pool draw is the very first draw <c>Mutate</c> makes, and
    /// <see cref="ScriptedRandomProvider"/> rejects a scripted value that falls outside the
    /// requested range, so the smallest rejected draw is exactly the pool size.
    /// </summary>
    private static int ObservePBestPoolSize(
        int populationSize,
        double pBestRate)
    {
        for (int draw = 0; draw < populationSize; draw++)
        {
            if (IsInsideThePBestPool(populationSize, pBestRate, draw) == false)
                return draw;
        }

        return populationSize;
    }

    private static bool IsInsideThePBestPool(
        int populationSize,
        double pBestRate,
        int pBestDraw)
    {
        try
        {
            RunMutation(populationSize, pBestRate, pBestDraw);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The script supplies exactly the four draws Mutate makes, and all of them but the
            // p-best draw are in range by construction, so the only way to get here is the
            // provider rejecting a p-best draw that sits outside the pool.
            return false;
        }
    }

    /// <summary>
    /// Runs one mutation over a genome-size-1 population in which individual <c>i</c> holds the
    /// value <c>i</c>, and returns the resulting trial gene. With F = 0.5, i = 0, r1 = 1 and
    /// r2 = 2 the trial collapses to <c>0.5 * x_pbest - 0.5</c>, which identifies x_pbest.
    /// </summary>
    private static double RunMutation(
        int populationSize,
        double pBestRate,
        int pBestDraw,
        int[]? ranking = null)
    {
        var population = new double[populationSize];
        for (int i = 0; i < populationSize; i++)
            population[i] = i;

        var trialIndividual = new double[1];
        double[] lowerBound = [-1_000.0];
        double[] upperBound = [1_000.0];

        // Draws consumed by Mutate, in order: x_pbest, r1, r2, then jrand for the single gene.
        // CR = 1 with one gene means the crossover makes no NextDouble draw at all.
        var random = new ScriptedRandomProvider(ints: [pBestDraw, 1, 2, 0]);

        var context = new MutationContext
        {
            IndividualIndex = 0,
            PopulationSize = populationSize,
            GenomeSize = 1,
            MutationForce = 0.5,
            CrossoverProbability = 1.0,
            Population = population,
            PopulationFfValues = population,
            TrialIndividual = trialIndividual,
            LowerBound = lowerBound,
            UpperBound = upperBound,
            RandomProvider = random,
            FitnessSortedIndices = ranking ?? Enumerable.Range(0, populationSize).ToArray()
        };

        new CurrentToPBestMutationStrategy(pBestRate).Mutate(context);

        return trialIndividual[0];
    }
}
