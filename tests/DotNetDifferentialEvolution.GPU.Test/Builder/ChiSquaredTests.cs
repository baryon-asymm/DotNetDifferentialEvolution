namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Positive controls for the χ² helper that check 1a's threshold comes from: each case holds the
/// helper against a closed form that shares no code with it.
/// </summary>
public class ChiSquaredTests
{
    /// <summary>
    /// With 2 degrees of freedom the CDF is <c>1 − e^(−x/2)</c>, so the 0.999 quantile is exactly
    /// <c>−2·ln(0.001)</c>.
    /// </summary>
    [Fact]
    public void TheQuantileForTwoDegreesOfFreedomIsMinusTwoLnOfTheTail()
    {
        var expected = -2.0 * Math.Log(0.001);

        var quantile = ChiSquared.Quantile(0.999, 2);

        Assert.Equal(expected, quantile, 1e-9);
    }

    /// <summary>
    /// With an even number of degrees of freedom 2m the CDF is
    /// <c>1 − e^(−x/2)·Σ_{k&lt;m} (x/2)^k / k!</c>, a finite sum. At the helper's 0.999 quantile for
    /// 20 degrees of freedom (one above check 1a's 19) that closed form gives 0.999.
    /// </summary>
    [Fact]
    public void TheQuantileForTwentyDegreesOfFreedomSatisfiesTheFiniteSumForm()
    {
        var quantile = ChiSquared.Quantile(0.999, 20);

        var half = quantile / 2.0;
        var term = 1.0;
        var sum = 1.0;
        for (var k = 1; k < 10; k++)
        {
            term *= half / k;
            sum += term;
        }

        Assert.Equal(0.999, 1.0 - Math.Exp(-half) * sum, 1e-12);
    }

    /// <summary>Γ(9.5), the half-integer behind 19 degrees of freedom, is <c>18! / (4⁹·9!)·√π</c>.</summary>
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

        Assert.Equal(Math.Log(expected), ChiSquared.LogGammaOfHalfInteger(9.5), 1e-12);
    }
}
