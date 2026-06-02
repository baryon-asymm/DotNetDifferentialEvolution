using DotNetDifferentialEvolution.PopulationSamplingMaker;

namespace DotNetDifferentialEvolution.UnitTests.PopulationSampling;

/// <summary>
/// Tests uniform random population initialization. The maker draws from <c>Random.Shared</c>,
/// so the assertions cover the structural invariants rather than exact values: every gene
/// lands within its own per-dimension bounds and the buffer is fully populated.
/// </summary>
[Trait("Category", "Unit")]
public class UniformRandomSamplingMakerTests
{
    [Fact]
    public void SamplesEveryGeneWithinItsPerDimensionBounds()
    {
        double[] lower = [-5.0, 10.0, 0.0];
        double[] upper = [5.0, 20.0, 1.0];
        var genomeSize = lower.Length;
        const int populationSize = 200;

        var population = new double[populationSize * genomeSize];
        var maker = new UniformRandomSamplingMaker(lower, upper);

        maker.SamplePopulation(population);

        for (int i = 0; i < population.Length; i++)
        {
            var gene = i % genomeSize;
            Assert.InRange(population[i], lower[gene], upper[gene]);
        }
    }

    [Fact]
    public void FillsTheEntireBuffer()
    {
        double[] lower = [1.0];
        double[] upper = [2.0];

        var population = new double[64];
        Array.Fill(population, double.NaN);

        new UniformRandomSamplingMaker(lower, upper).SamplePopulation(population);

        Assert.DoesNotContain(population, double.IsNaN);
    }
}
