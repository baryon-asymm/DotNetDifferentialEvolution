using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>The random inputs and the bitwise comparison the parity checks share (ACCEPTANCE.md, 1g, S2–S4).</summary>
internal static class ParityCases
{
    /// <summary>
    /// A box per gene and a population inside it: lower bounds in [−10, 10), widths in [0.5, 10.5), so a mutant with F
    /// up to 2 often leaves the box and the repair runs.
    /// </summary>
    /// <param name="random">The case generator.</param>
    /// <param name="populationSize">N.</param>
    /// <param name="genomeSize">D.</param>
    /// <returns>The bounds and the population.</returns>
    public static (double[] Lower, double[] Upper, double[] Population) Box(SeededRandomProvider random, int populationSize, int genomeSize)
    {
        var lower = new double[genomeSize];
        var upper = new double[genomeSize];
        for (var j = 0; j < genomeSize; j++)
        {
            lower[j] = -10.0 + 20.0 * random.NextDouble();
            upper[j] = lower[j] + 0.5 + 10.0 * random.NextDouble();
        }

        var population = new double[populationSize * genomeSize];
        for (var k = 0; k < population.Length; k++)
        {
            var j = k % genomeSize;
            population[k] = lower[j] + random.NextDouble() * (upper[j] - lower[j]);
        }

        return (lower, upper, population);
    }

    /// <summary>Asserts two vectors equal bit for bit, naming the first gene that differs.</summary>
    /// <param name="expected">The CPU's values.</param>
    /// <param name="actual">The GPU's values.</param>
    /// <param name="what">The case, for the message.</param>
    public static void AssertSameBits(double[] expected, double[] actual, string what)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var j = 0; j < expected.Length; j++)
        {
            AssertSameBits(expected[j], actual[j], $"{what}, gene {j}");
        }
    }

    /// <summary>Asserts two doubles equal bit for bit.</summary>
    /// <param name="expected">The CPU's value.</param>
    /// <param name="actual">The GPU's value.</param>
    /// <param name="what">The value, for the message.</param>
    public static void AssertSameBits(double expected, double actual, string what) =>
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{what}: CPU {expected:R}, GPU {actual:R}");
}
