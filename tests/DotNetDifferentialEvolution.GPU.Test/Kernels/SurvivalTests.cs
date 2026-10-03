using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 1e: <see cref="DeStep.Survives"/> gives the survivor of the CPU package's
/// <c>SelectionStrategyTests</c> (<c>tests/DotNetDifferentialEvolution.UnitTests/SelectionStrategies</c>),
/// with that file's fitness values: parent 9, trial 1, 50, 9 or <see cref="double.NaN"/>. Each test
/// names the CPU case it mirrors. Of the nine CPU cases, eight apply: the GPU package has only the
/// rule <c>f(u) ≤ f(x)</c>, with no "ties rejected" mode, so
/// <c>WithTiesRejectedKeepsTheParentOnEqualFitness</c> has no counterpart; the other two
/// ties-rejected cases have the same survivor under the GPU rule and are mirrored.
/// </summary>
[Trait("Category", "Unit")]
public class SurvivalTests
{
    private const double ParentFitness = 9.0;

    /// <summary>Mirrors <c>AcceptsTrialWhenStrictlyBetter</c>: trial 1 against parent 9 survives.</summary>
    [Fact]
    public void AStrictlyBetterTrialSurvives() => Assert.True(DeStep.Survives(1.0, ParentFitness));

    /// <summary>Mirrors <c>KeepsParentWhenTrialIsWorse</c>: trial 50 against parent 9 does not.</summary>
    [Fact]
    public void AWorseTrialDoesNotSurvive() => Assert.False(DeStep.Survives(50.0, ParentFitness));

    /// <summary>Mirrors <c>TakesTheTrialWhenFitnessIsEqualButDoesNotCallItAnImprovement</c>: a tie takes the trial.</summary>
    [Fact]
    public void ATieTakesTheTrial() => Assert.True(DeStep.Survives(9.0, ParentFitness));

    /// <summary>Mirrors <c>WithTiesRejectedStillTakesAStrictlyBetterTrial</c>: the survivor is the trial.</summary>
    [Fact]
    public void AStrictlyBetterTrialSurvivesAsWithTiesRejected() => Assert.True(DeStep.Survives(1.0, ParentFitness));

    /// <summary>Mirrors <c>WithTiesRejectedAParentScoredNaNIsStillReplaced</c>: trial 9 replaces a NaN parent.</summary>
    [Fact]
    public void AParentScoredNaNIsReplacedAsWithTiesRejected() => Assert.True(DeStep.Survives(9.0, double.NaN));

    /// <summary>Mirrors <c>AcceptsTrialWhenParentFitnessIsNaN</c>: trial 50 replaces a NaN parent.</summary>
    [Fact]
    public void AParentScoredNaNIsReplaced() => Assert.True(DeStep.Survives(50.0, double.NaN));

    /// <summary>Mirrors <c>KeepsParentWhenTrialFitnessIsNaN</c>: a NaN trial does not replace parent 9.</summary>
    [Fact]
    public void ATrialScoredNaNDoesNotSurvive() => Assert.False(DeStep.Survives(double.NaN, ParentFitness));

    /// <summary>Mirrors <c>KeepsParentWhenBothFitnessValuesAreNaN</c>: two NaNs are not a tie, the parent stays.</summary>
    [Fact]
    public void TwoNaNsKeepTheParent() => Assert.False(DeStep.Survives(double.NaN, double.NaN));
}
