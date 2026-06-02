using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.RandomProviders;

/// <summary>
/// Tests the Box–Muller (Gaussian) and inverse-CDF (Cauchy) samplers that drive JADE/SHADE
/// parameter adaptation. A <see cref="ScriptedRandomProvider"/> supplies the underlying
/// uniform draws, so each result is a deterministic closed-form value.
/// </summary>
[Trait("Category", "Unit")]
public class RandomDistributionHelperTests
{
    private const double Precision = 1e-9;

    [Fact]
    public void NextGaussian_MatchesBoxMullerClosedForm()
    {
        // Draws 0.75, 0.0 → u1 = 1 - 0.75 = 0.25, u2 = 1 - 0.0 = 1.0.
        // z = sqrt(-2 ln u1) * cos(2π u2) = sqrt(-2 ln 0.25) * cos(2π) = 1.66510922...
        var random = new ScriptedRandomProvider(doubles: [0.75, 0.0]);

        var value = RandomDistributionHelper.NextGaussian(random, mean: 2.0, standardDeviation: 3.0);

        var z = Math.Sqrt(-2.0 * Math.Log(0.25)) * Math.Cos(2.0 * Math.PI);
        Assert.Equal(2.0 + 3.0 * z, value, Precision);
        Assert.Equal(2, random.DoubleDrawCount); // consumes exactly two uniforms
    }

    [Fact]
    public void NextGaussian_WithZeroDeviation_ReturnsMean()
    {
        var random = new ScriptedRandomProvider(doubles: [0.3, 0.6]);

        var value = RandomDistributionHelper.NextGaussian(random, mean: 5.0, standardDeviation: 0.0);

        Assert.Equal(5.0, value, Precision);
    }

    [Fact]
    public void NextCauchy_AtMedianDrawReturnsLocation()
    {
        // u = 0.5 → tan(π(0.5 - 0.5)) = tan(0) = 0 → location.
        var random = new ScriptedRandomProvider(doubles: [0.5]);

        var value = RandomDistributionHelper.NextCauchy(random, location: 2.0, scale: 7.0);

        Assert.Equal(2.0, value, Precision);
    }

    [Fact]
    public void NextCauchy_AtUpperQuartileReturnsLocationPlusScale()
    {
        // u = 0.75 → tan(π·0.25) = 1 → location + scale.
        var random = new ScriptedRandomProvider(doubles: [0.75]);

        var value = RandomDistributionHelper.NextCauchy(random, location: 1.0, scale: 2.0);

        Assert.Equal(3.0, value, Precision);
    }
}
