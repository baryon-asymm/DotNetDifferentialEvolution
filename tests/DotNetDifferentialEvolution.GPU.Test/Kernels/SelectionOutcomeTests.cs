using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check S5: <see cref="Selection.Outcome"/> reports what the CPU package's
/// <see cref="SelectionStrategy"/> reports, with ties accepted (the fixed schemes, SHADE, L-SHADE) and refused
/// (jDE, JADE). The nine cases of the CPU package's <c>SelectionStrategyTests</c> by name, and every pair of seven fitness
/// values (−∞, −1, −0, 0, 1, +∞, NaN) in both modes.
/// </summary>
[Trait("Category", "Unit")]
public class SelectionOutcomeTests
{
    private const double ParentFitness = 9.0;

    private static readonly double[] Values = [double.NegativeInfinity, -1.0, -0.0, 0.0, 1.0, double.PositiveInfinity, double.NaN];

    /// <summary>The CPU package's nine cases: (trial, parent, ties accepted, the outcome).</summary>
    /// <returns>The cases.</returns>
    public static TheoryData<double, double, bool, int> NineCases() => new()
    {
        { 1.0, ParentFitness, true, Selection.Improved },
        { 50.0, ParentFitness, true, Selection.Kept },
        { ParentFitness, ParentFitness, true, Selection.Accepted },
        { ParentFitness, ParentFitness, false, Selection.Kept },
        { 1.0, ParentFitness, false, Selection.Improved },
        { ParentFitness, double.NaN, false, Selection.Improved },
        { 50.0, double.NaN, true, Selection.Improved },
        { double.NaN, ParentFitness, true, Selection.Kept },
        { double.NaN, double.NaN, true, Selection.Kept },
    };

    /// <summary>Each of the CPU package's nine cases gives its outcome.</summary>
    /// <param name="trialFitness">f(u).</param>
    /// <param name="parentFitness">f(x).</param>
    /// <param name="acceptsTies">Whether ties are accepted.</param>
    /// <param name="expected">The outcome.</param>
    [Theory]
    [MemberData(nameof(NineCases))]
    public void TheNineCasesGiveTheirOutcomes(double trialFitness, double parentFitness, bool acceptsTies, int expected)
    {
        Assert.Equal(expected, Selection.Outcome(trialFitness, parentFitness, acceptsTies));
        Assert.Equal(expected, CpuOutcome(trialFitness, parentFitness, acceptsTies));
    }

    /// <summary>Every pair of the seven values, in both modes, gives the CPU strategy's outcome.</summary>
    [Fact]
    public void EveryPairGivesTheCpuStrategysOutcome()
    {
        foreach (var acceptsTies in new[] { true, false })
        {
            foreach (var trial in Values)
            {
                foreach (var parent in Values)
                {
                    Assert.True(
                        CpuOutcome(trial, parent, acceptsTies) == Selection.Outcome(trial, parent, acceptsTies),
                        $"trial {trial}, parent {parent}, ties accepted {acceptsTies}");
                }
            }
        }
    }

    private static int CpuOutcome(double trialFitness, double parentFitness, bool acceptsTies)
    {
        var outcome = new SelectionStrategy(1, acceptsTies).SelectSurvivor(
            individualIndex: 0,
            trialIndividualFfValue: trialFitness,
            trialIndividual: new double[1],
            populationFfValues: [parentFitness],
            population: new double[1],
            nextPopulationFfValues: new double[1],
            nextPopulation: new double[1]);
        return outcome switch
        {
            SelectionOutcome.TrialImproved => Selection.Improved,
            SelectionOutcome.TrialAccepted => Selection.Accepted,
            SelectionOutcome.ParentKept => Selection.Kept,
            _ => throw new ArgumentOutOfRangeException(nameof(trialFitness), outcome, "Not an outcome."),
        };
    }
}
