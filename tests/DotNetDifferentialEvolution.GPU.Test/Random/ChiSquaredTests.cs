namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// Positive controls of <see cref="ChiSquared"/>, the quantile helper of checks 1b and 3b: its
/// answers against closed forms that share none of its code. With an even number of degrees of
/// freedom <c>2n</c>, the χ² CDF is <c>1 − e^(−x/2) Σ_{k&lt;n} (x/2)^k / k!</c> (the Poisson tail);
/// with 2 it is <c>1 − e^(−x/2)</c>, whose 0.999 quantile is exactly <c>−2 ln 0.001</c>.
/// </summary>
[Trait("Category", "Unit")]
public class ChiSquaredTests
{
    /// <summary>
    /// Γ(9.5), the half-integer behind 19 degrees of freedom (check 1a), is <c>18! / (4⁹·9!)·√π</c>: the odd
    /// branch of <see cref="ChiSquared.LogGammaOfHalf"/> against a closed form it shares no code with.
    /// </summary>
    [Fact]
    public void GammaOfNineAndAHalfMatchesTheDoubleFactorialForm()
    {
        var factorial18 = 1.0;
        for (var k = 2; k <= 18; k++)
        {
            factorial18 *= k;
        }

        var factorial9 = 1.0;
        for (var k = 2; k <= 9; k++)
        {
            factorial9 *= k;
        }

        var expected = factorial18 / (Math.Pow(4.0, 9.0) * factorial9) * Math.Sqrt(Math.PI);

        Assert.Equal(Math.Log(expected), ChiSquared.LogGammaOfHalf(19), 1e-12);
    }

    /// <summary>df = 2: the 0.999 quantile is <c>−2 ln(0.001)</c> and the CDF is <c>1 − e^(−x/2)</c>.</summary>
    [Fact]
    public void TwoDegreesOfFreedomMatchTheClosedForm()
    {
        var expected = -2.0 * Math.Log(0.001);

        var quantile = ChiSquared.Quantile(0.999, 2);

        Assert.Equal(expected, quantile, expected * 1e-12);
        foreach (var x in new[] { 0.1, 1.0, 2.5, 7.0, 13.8155, 30.0 })
        {
            Assert.Equal(1.0 - Math.Exp(-x / 2.0), ChiSquared.Cdf(x, 2), 1e-13);
        }
    }

    /// <summary>
    /// The quantiles the checks use with an even df (8, 48, 98, 100, 2400) sit where the Poisson-tail
    /// closed form gives 0.999, and the odd df 99 of check 3b lies between those of 98 and 100.
    /// </summary>
    /// <param name="degreesOfFreedom">An even number of degrees of freedom.</param>
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(48)]
    [InlineData(98)]
    [InlineData(100)]
    [InlineData(2400)]
    public void EvenDegreesOfFreedomMatchThePoissonTail(int degreesOfFreedom)
    {
        var quantile = ChiSquared.Quantile(0.999, degreesOfFreedom);

        Assert.Equal(0.999, PoissonTailCdf(quantile, degreesOfFreedom), 1e-10);
        foreach (var x in new[] { degreesOfFreedom * 0.5, degreesOfFreedom * 1.0, degreesOfFreedom * 1.5 })
        {
            Assert.Equal(PoissonTailCdf(x, degreesOfFreedom), ChiSquared.Cdf(x, degreesOfFreedom), 1e-10);
        }
    }

    /// <summary>The df-99 quantile of check 3b lies strictly between those of df 98 and df 100.</summary>
    [Fact]
    public void OddDegreesOfFreedomLieBetweenTheirEvenNeighbours()
    {
        var below = ChiSquared.Quantile(0.999, 98);
        var quantile = ChiSquared.Quantile(0.999, 99);
        var above = ChiSquared.Quantile(0.999, 100);

        Assert.True(below < quantile && quantile < above, $"{below} < {quantile} < {above}");
    }

    private static double PoissonTailCdf(double x, int degreesOfFreedom)
    {
        var half = x / 2.0;
        var logFactorial = 0.0;
        var sum = 0.0;
        for (var k = 0; k < degreesOfFreedom / 2; k++)
        {
            if (k > 0)
            {
                logFactorial += Math.Log(k);
            }

            sum += Math.Exp(-half + k * Math.Log(half) - logFactorial);
        }

        return 1.0 - sum;
    }
}
