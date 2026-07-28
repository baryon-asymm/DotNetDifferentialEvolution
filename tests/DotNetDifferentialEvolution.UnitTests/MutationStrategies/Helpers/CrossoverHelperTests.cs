using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.RandomProviders;
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
        // i1=0.9 (>0.5 → copy parent 200, so the OOB -1 is never repaired),
        // i3=0.1 (<=0.5 → crossover; 999 > upper → midpoint (10 + parent 400)/2 = 205).
        var random = new ScriptedRandomProvider(
            ints: [2],
            doubles: [0.4, 0.9, 0.1]);

        CrossoverHelper.BinomialCrossoverAndRepair(
            individualIndex: 0,
            crossoverProbability: 0.5,
            population: population,
            trialIndividual: trial,
            lowerBound: lower,
            upperBound: upper,
            randomSource: new ProviderRandomSource(random));

        Assert.Equal(new[] { 5.0, 200.0, 7.0, 205.0 }, trial, new DoubleComparer(Precision));
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
            randomSource: new ProviderRandomSource(random));

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
            randomSource: new ProviderRandomSource(random));

        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, trial, new DoubleComparer(Precision));
    }

    [Fact]
    public void RepairReflectsOutOfBoundGenesHalfwayTowardTheParent()
    {
        // Parent (individual 0).
        double[] population = [100.0, 200.0, 300.0, 400.0];
        double[] lower = [0.0, 0.0, 0.0, 0.0];
        double[] upper = [10.0, 10.0, 10.0, 10.0];

        // Mutant: gene0 below lower, gene1 in-bounds, gene2 in-bounds, gene3 above upper.
        double[] trial = [-1.0, 5.0, 7.0, 999.0];

        // jrand = gene 0 (guaranteed from mutant; -1 < lower → midpoint (0 + 100)/2 = 50, no draw).
        // CR draws for genes 1..3: i1=0.0 (keep mutant 5), i2=0.9 (copy parent 300),
        // i3=0.0 (crossover; 999 > upper → midpoint (10 + 400)/2 = 205, no draw).
        var random = new ScriptedRandomProvider(
            ints: [0],
            doubles: [0.0, 0.9, 0.0]);

        CrossoverHelper.BinomialCrossoverAndRepair(
            individualIndex: 0,
            crossoverProbability: 0.5,
            population: population,
            trialIndividual: trial,
            lowerBound: lower,
            upperBound: upper,
            randomSource: new ProviderRandomSource(random));

        Assert.Equal(new[] { 50.0, 5.0, 300.0, 205.0 }, trial, new DoubleComparer(Precision));
        // Midpoint repair is deterministic: it consumes no random draws beyond the CR tests.
        Assert.Equal(3, random.DoubleDrawCount);
    }

    [Theory]
    [InlineData(0.0, 5)]
    [InlineData(0.1, 5)]
    [InlineData(0.5, 10)]
    [InlineData(0.9, 30)]
    [InlineData(1.0, 30)]
    public void GeneInheritanceRateMatchesTheClosedForm(
        double crossoverProbability,
        int genomeSize)
    {
        // The per-gene test is now an integer comparison against a scaled threshold rather than
        // a floating-point one against a fresh uniform. Whether that is the same algorithm is
        // decided here: a gene comes from the mutant if the crossover fires or it is jrand, so
        // the rate is CR + (1 - CR)/D, and nothing about the reformulation may move it.
        const int Trials = 20_000;

        var random = new SeededRandomProvider(seed: genomeSize);
        var population = new double[genomeSize];      // parent: all zeros
        var trial = new double[genomeSize];
        var lowerBound = new double[genomeSize];
        var upperBound = new double[genomeSize];
        Array.Fill(lowerBound, -1_000.0);
        Array.Fill(upperBound, 1_000.0);

        var fromMutant = 0L;
        for (int t = 0; t < Trials; t++)
        {
            Array.Fill(trial, 1.0);                   // mutant: all ones, all in bounds

            CrossoverHelper.BinomialCrossoverAndRepair(
                individualIndex: 0,
                crossoverProbability: crossoverProbability,
                population: population,
                trialIndividual: trial,
                lowerBound: lowerBound,
                upperBound: upperBound,
                randomSource: new SeededRandomSource(random));

            foreach (var gene in trial)
                fromMutant += gene == 1.0 ? 1 : 0;
        }

        var observed = (double)fromMutant / (Trials * (long)genomeSize);
        var expected = crossoverProbability + (1.0 - crossoverProbability) / genomeSize;

        // ~3.5 standard errors of the binomial at this sample size, so a real shift in the rate
        // fails while sampling noise does not.
        var standardError = Math.Sqrt(expected * (1.0 - expected) / (Trials * (double)genomeSize));
        Assert.Equal(expected, observed, 3.5 * standardError + 1e-12);
    }

    [Fact]
    public void TheGuaranteedGeneIsUniformlyDistributedOverTheGenome()
    {
        // jrand is drawn with Next(genomeSize). If that were biased, the crossover rate above
        // would still be met on average while some genes were systematically favoured.
        const int GenomeSize = 16;
        const int Trials = 32_000;

        var random = new SeededRandomProvider(seed: 99);
        var population = new double[GenomeSize];
        var trial = new double[GenomeSize];
        var lowerBound = new double[GenomeSize];
        var upperBound = new double[GenomeSize];
        Array.Fill(lowerBound, -1_000.0);
        Array.Fill(upperBound, 1_000.0);

        var counts = new int[GenomeSize];
        for (int t = 0; t < Trials; t++)
        {
            Array.Fill(trial, 1.0);

            // CR = 0, so the only gene that can survive from the mutant is jrand itself.
            CrossoverHelper.BinomialCrossoverAndRepair(
                individualIndex: 0,
                crossoverProbability: 0.0,
                population: population,
                trialIndividual: trial,
                lowerBound: lowerBound,
                upperBound: upperBound,
                randomSource: new SeededRandomSource(random));

            for (int i = 0; i < GenomeSize; i++)
            {
                if (trial[i] == 1.0)
                    counts[i]++;
            }
        }

        var expected = (double)Trials / GenomeSize;
        var chiSquare = counts.Sum(count => (count - expected) * (count - expected) / expected);

        var degreesOfFreedom = GenomeSize - 1;
        var critical = degreesOfFreedom + 4.0 * Math.Sqrt(2.0 * degreesOfFreedom);

        Assert.Equal(Trials, counts.Sum());   // exactly one gene per trial, never zero or two
        Assert.True(chiSquare < critical, $"chi-square {chiSquare:F2} exceeded {critical:F2}");
    }

    private sealed class DoubleComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => obj.GetHashCode();
    }
}
