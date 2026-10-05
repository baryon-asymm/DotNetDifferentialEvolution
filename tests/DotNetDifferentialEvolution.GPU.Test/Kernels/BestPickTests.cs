namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 1f: <c>BestPick.IndexOf</c> ranks <see cref="double.NaN"/> worst and gives a tie
/// to the lowest index (<c>docs/ALGORITHMS.md</c>, §9.2). <c>BestPick</c> sits at the package root; it
/// is checked here, with the survival rule it mirrors.
/// </summary>
[Trait("Category", "Unit")]
public class BestPickTests
{
    /// <summary>Fitness <c>[NaN, 3, 1, 1]</c> gives index 2: past the NaN at 0, the first of the tied ones.</summary>
    [Fact]
    public void ANaNFirstAndATieGiveTheFirstOfTheTiedBest() => Assert.Equal(2, BestPick.IndexOf([double.NaN, 3.0, 1.0, 1.0]));
}
