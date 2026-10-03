namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Positive controls for the ULP distance D2 measures with, on neighbours obtained from
/// <see cref="Math.BitIncrement"/> and <see cref="Math.BitDecrement"/>, which share no code with it.
/// </summary>
public class UlpTests
{
    /// <summary>Equal values, and +0 against −0, are 0 apart.</summary>
    [Fact]
    public void EqualValuesAreZeroApart()
    {
        Assert.Equal(0UL, Ulp.Distance(1.37, 1.37));
        Assert.Equal(0UL, Ulp.Distance(0.0, -0.0));
    }

    /// <summary>A value and its next representable neighbour are 1 apart, in both orders, at 1, at 700 and below 0.</summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(700.0)]
    [InlineData(-3.5)]
    [InlineData(1e-300)]
    public void NeighboursAreOneApart(double value)
    {
        Assert.Equal(1UL, Ulp.Distance(value, Math.BitIncrement(value)));
        Assert.Equal(1UL, Ulp.Distance(Math.BitDecrement(value), value));
    }

    /// <summary>Four steps up are 4 apart, across the exponent boundary at 2.</summary>
    [Fact]
    public void StepsAddUpAcrossAnExponentBoundary()
    {
        var value = Math.BitDecrement(Math.BitDecrement(2.0));
        var fourUp = Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(value))));

        Assert.Equal(4UL, Ulp.Distance(value, fourUp));
    }

    /// <summary>The smallest negative and the smallest positive subnormal are 2 apart: −ε, ±0, +ε.</summary>
    [Fact]
    public void TheCountIsRightAcrossZero() => Assert.Equal(2UL, Ulp.Distance(-double.Epsilon, double.Epsilon));

    /// <summary><see cref="double.NaN"/> is infinitely far from everything.</summary>
    [Fact]
    public void NaNIsAsFarAsItGets() => Assert.Equal(ulong.MaxValue, Ulp.Distance(double.NaN, 1.0));
}
