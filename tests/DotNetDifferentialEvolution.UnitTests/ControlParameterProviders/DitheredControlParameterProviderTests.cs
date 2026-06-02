using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.ControlParameterProviders;

/// <summary>
/// Tests the dithered provider: F is sampled uniformly from [min, max] per individual while
/// CR stays constant.
/// </summary>
[Trait("Category", "Unit")]
public class DitheredControlParameterProviderTests
{
    [Fact]
    public void SamplesMutationForceWithinRangeFromTheRandomDraw()
    {
        // F = min + draw * (max - min) = 0.3 + draw * 0.6.
        var provider = new DitheredControlParameterProvider(
            minMutationForce: 0.3, maxMutationForce: 0.9, crossoverProbability: 0.85);
        var random = new ScriptedRandomProvider(doubles: [0.0, 0.5, 1.0]);

        provider.GetControlParameters(0, random, out var f0, out var cr0);
        provider.GetControlParameters(1, random, out var f1, out var cr1);
        provider.GetControlParameters(2, random, out var f2, out var _);

        Assert.Equal(0.3, f0, 1e-12);   // draw 0.0 → min
        Assert.Equal(0.6, f1, 1e-12);   // draw 0.5 → midpoint
        Assert.Equal(0.9, f2, 1e-12);   // draw 1.0 → max
        Assert.Equal(0.85, cr0);        // CR is constant
        Assert.Equal(0.85, cr1);
    }

    [Fact]
    public void ConstructorThrowsWhenMinExceedsMax()
    {
        Assert.Throws<ArgumentException>(() =>
            new DitheredControlParameterProvider(minMutationForce: 0.9, maxMutationForce: 0.3, crossoverProbability: 0.5));
    }

    [Fact]
    public void AllowsEqualMinAndMax()
    {
        var provider = new DitheredControlParameterProvider(
            minMutationForce: 0.5, maxMutationForce: 0.5, crossoverProbability: 0.5);
        var random = new ScriptedRandomProvider(doubles: [0.123]);

        provider.GetControlParameters(0, random, out var f, out _);

        Assert.Equal(0.5, f, 1e-12); // zero range → always min regardless of the draw
    }
}
