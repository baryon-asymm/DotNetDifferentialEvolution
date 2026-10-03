using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 1d: midpoint repair (<c>docs/ALGORITHMS.md</c>, §3.3), through
/// <see cref="DeStep.BuildTrial"/> on scripted draws. Individual 1 of four, five genes, F = 1 and
/// <c>x_r2 = x_r3</c>, so the mutant is exactly <c>x_r1 = (−4, 14, 4, 0.25, 10)</c>; CR = 1, so every
/// gene comes from the mutant. Parent <c>(1, 9, 5, 5, 5)</c>; box <c>[−2, 10] × [0, 12] × [0, 10]³</c>.
/// </summary>
[Trait("Category", "Unit")]
public class RepairTests
{
    private const int Individual = 1;

    private static readonly double[] Population =
    [
        -4.0, 14.0, 4.0, 0.25, 10.0,
        1.0, 9.0, 5.0, 5.0, 5.0,
        0.0, 0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0, 0.0,
    ];

    private static readonly double[] Lower = [-2.0, 0.0, 0.0, 0.0, 0.0];
    private static readonly double[] Upper = [10.0, 12.0, 10.0, 10.0, 10.0];

    /// <summary>Gene 0, mutant −4 below the bound −2, becomes exactly <c>(−2 + 1) / 2 = −0.5</c>.</summary>
    [Fact]
    public void AGeneBelowTheBoundBecomesTheMidpointWithTheParent() => Assert.Equal(-0.5, Build()[0]);

    /// <summary>Gene 1, mutant 14 above the bound 12, becomes exactly <c>(12 + 9) / 2 = 10.5</c>.</summary>
    [Fact]
    public void AGeneAboveTheBoundBecomesTheMidpointWithTheParent() => Assert.Equal(10.5, Build()[1]);

    /// <summary>Genes 2 to 4, mutants 4, 0.25 and 10 (the last on the upper bound), are untouched.</summary>
    [Fact]
    public void AnInBoxMutantGeneIsUntouched()
    {
        var trial = Build();

        Assert.Equal(4.0, trial[2]);
        Assert.Equal(0.25, trial[3]);
        Assert.Equal(10.0, trial[4]);
    }

    private static double[] Build()
    {
        // Donors for i = 1 out of N − 1 = 3 others: 0 → r1 = 0; 1 → skips i → r2 = 2; 2 → r3 = 3.
        var draws = new ScriptedDraws(
        [
            ScriptedDraw.Index(0, 3),
            ScriptedDraw.Index(1, 3),
            ScriptedDraw.Index(2, 3),
            ScriptedDraw.Index(0, 5),
            ScriptedDraw.Word(ulong.MaxValue),
            ScriptedDraw.Word(0UL),
            ScriptedDraw.Word(1UL),
            ScriptedDraw.Word(ulong.MaxValue),
        ]);
        using var step = new HostStep();

        var trial = step.BuildTrial(ref draws, Individual, mutationForce: 1.0, crossoverProbability: 1.0, Population, Lower, Upper);

        Assert.Equal(8, draws.Consumed);
        return trial;
    }
}
