using DotNetDifferentialEvolution.PopulationSamplingMaker;
using DotNetDifferentialEvolution.Tests.Common.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.PopulationSampling;

/// <summary>
/// Tests per-dimension bounds and buffer coverage, with scripted draws for exact arithmetic checks.
/// </summary>
[Trait("Category", "Unit")]
public class UniformRandomSamplingMakerTests
{
    /// <summary>Each draw gives the expected gene, including when the width overflows or arithmetic rounds past the box.</summary>
    /// <param name="lower">The lower endpoint.</param>
    /// <param name="upper">The upper endpoint.</param>
    /// <param name="draw">The scripted unit draw.</param>
    /// <param name="expected">The rounded, in-box result.</param>
    [Theory]
    [InlineData(-1e308, 1e308, 0.0, -1e308)]
    [InlineData(-1e308, 1e308, 0.5, 0.0)]
    [InlineData(-1e308, 1e308, 0.75, 5e307)]
    [InlineData(-1.0, 9.0, 0.3, 2.0)]
    [InlineData(double.MaxValue, double.MaxValue, 0.75, double.MaxValue)]
    [InlineData(1.0, 1.0000000000000002, 0.75, 1.0000000000000002)]
    [InlineData(-1.0, -1e-17, 1.0, -1e-17)]
    public void SamplesFiniteIntervals(double lower, double upper, double draw, double expected)
    {
        var random = new ScriptedRandomProvider(doubles: [draw]);
        var maker = new UniformRandomSamplingMaker(new[] { lower }, new[] { upper }, random);
        var population = new double[1];

        maker.SamplePopulation(population);

        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(population[0]));
        Assert.Equal(1, random.DoubleDrawCount);
    }

    /// <summary>
    /// Every sampled gene lies within the bounds of its own dimension.
    /// </summary>
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

        for (var i = 0; i < population.Length; i++)
        {
            var gene = i % genomeSize;
            Assert.InRange(population[i], lower[gene], upper[gene]);
        }
    }

    /// <summary>
    /// Sampling overwrites every element of the population buffer.
    /// </summary>
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
