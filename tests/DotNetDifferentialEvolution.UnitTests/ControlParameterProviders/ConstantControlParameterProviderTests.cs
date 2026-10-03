using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.Tests.Common.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.ControlParameterProviders;

/// <summary>
/// Tests the constant F/CR provider used by classic DE.
/// </summary>
[Trait("Category", "Unit")]
public class ConstantControlParameterProviderTests
{
    /// <summary>
    /// Every individual receives the configured F and CR, and the random provider is never consulted.
    /// </summary>
    [Fact]
    public void ReturnsTheSameParametersForEveryIndividual()
    {
        var provider = new ConstantControlParameterProvider(mutationForce: 0.7, crossoverProbability: 0.9);
        var random = new ScriptedRandomProvider(); // must not be consulted

        foreach (var index in new[] { 0, 1, 17, 99 })
        {
            provider.GetControlParameters(index, random, out var f, out var cr);

            Assert.Equal(0.7, f);
            Assert.Equal(0.9, cr);
        }

        Assert.Equal(0, random.DoubleDrawCount);
        Assert.Equal(0, random.IntDrawCount);
    }
}
