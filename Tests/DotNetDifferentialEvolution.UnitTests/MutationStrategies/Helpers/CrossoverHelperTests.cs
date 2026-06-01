using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.MutationStrategies.Helpers;

/// <summary>
/// White-box tests for binomial crossover + bound repair. A <see cref="ScriptedRandomProvider"/>
/// dictates the guaranteed-gene index (jrand), the per-gene crossover draws, and the repair
/// draws, so the exact resulting trial vector is predictable.
/// </summary>
[Trait("Category", "Unit")]
public class CrossoverHelperTests
{
    private const double Precision = 1e-12;

    [Fact]
    public void MixesMutantAndParentGenesAndRepairsOutOfBounds()
    {
        // Parent (individual 0). Distinct values so a copied gene is unmistakable.
        double[] population = [100.0, 200.0, 300.0, 400.0];
        double[] lower = [0.0, 0.0, 0.0, 0.0];
        double[] upper = [10.0, 10.0, 10.0, 10.0];

        // Mutant: gene0 in-bounds, gene1 below lower, gene2 in-bounds, gene3 above upper.
        double[] trial = [5.0, -1.0, 7.0, 999.0];

        // jrand = gene 2 (guaranteed from mutant, no CR draw for it).
        // CR draws (in gene order, skipping jrand): i0=0.4 (<=0.5 → keep mutant 5),
        // i1=0.9 (>0.5 → copy parent 200), i3=0.1 (<=0.5 → crossover; 999 OOB → repair),
        // repair draw 0.5 → 0.5*(10-0)+0 = 5.0.
        var random = new ScriptedRandomProvider(
            ints: [2],
            doubles: [0.4, 0.9, 0.1, 0.5]);

        CrossoverHelper.BinomialCrossoverAndRepair(
            individualIndex: 0,
            crossoverProbability: 0.5,
            population: population,
            trialIndividual: trial,
            lowerBound: lower,
            upperBound: upper,
            randomProvider: random);

        Assert.Equal(new[] { 5.0, 200.0, 7.0, 5.0 }, trial, new DoubleComparer(Precision));
    }

    [Fact]
    public void GuaranteedGeneAlwaysComesFromMutant_EvenWhenCrossoverNeverFires()
    {
        // CR = 0 means the CR test (NextDouble() <= 0) is effectively always false, so every
        // non-jrand gene is copied from the parent. The jrand gene must still take the mutant.
        double[] population = [10.0, 20.0, 30.0];
        double[] lower = [0.0, 0.0, 0.0];
        double[] upper = [100.0, 100.0, 100.0];
        double[] trial = [1.0, 2.0, 3.0];

        var random = new ScriptedRandomProvider(
            ints: [1],                 // jrand = gene 1
            doubles: [0.5, 0.5]);      // CR draws for genes 0 and 2 (both > 0 → copy parent)

        CrossoverHelper.BinomialCrossoverAndRepair(
            individualIndex: 0,
            crossoverProbability: 0.0,
            population: population,
            trialIndividual: trial,
            lowerBound: lower,
            upperBound: upper,
            randomProvider: random);

        // Genes 0 and 2 copied from parent; gene 1 (jrand) kept from mutant — the trial
        // differs from its parent in at least one dimension, the canonical guarantee.
        Assert.Equal(new[] { 10.0, 2.0, 30.0 }, trial, new DoubleComparer(Precision));
    }

    [Fact]
    public void InBoundsMutantGenesAreKeptWhenCrossoverAlwaysFires()
    {
        double[] population = [10.0, 20.0, 30.0];
        double[] lower = [0.0, 0.0, 0.0];
        double[] upper = [100.0, 100.0, 100.0];
        double[] trial = [1.0, 2.0, 3.0];

        // CR = 1 → crossover fires for every gene; all mutant genes are in-bounds so none are
        // repaired and the trial equals the (in-bounds) mutant.
        var random = new ScriptedRandomProvider(
            ints: [0],
            doubles: [0.0, 0.0]);   // CR draws for genes 1 and 2 (gene 0 is jrand)

        CrossoverHelper.BinomialCrossoverAndRepair(
            individualIndex: 0,
            crossoverProbability: 1.0,
            population: population,
            trialIndividual: trial,
            lowerBound: lower,
            upperBound: upper,
            randomProvider: random);

        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, trial, new DoubleComparer(Precision));
    }

    private sealed class DoubleComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => obj.GetHashCode();
    }
}
