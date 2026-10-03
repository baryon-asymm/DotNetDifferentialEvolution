using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 1c: binomial crossover with <c>jrand</c> (<c>docs/ALGORITHMS.md</c>, §3.2),
/// through <see cref="DeStep.BuildTrial"/> on scripted draws. Individual 1 of four, five genes, F = ½;
/// the scripted donors are r1 = 0, r2 = 2, r3 = 3, so the mutant is <c>(2, 3, 4, 5, 6)</c> exactly and
/// the parent is <c>(10, 20, 30, 40, 50)</c>; <c>jrand</c> = 2. The box is wide: nothing is repaired.
/// </summary>
[Trait("Category", "Unit")]
public class CrossoverTests
{
    private const int Individual = 1;
    private const int GuaranteedGene = 2;
    private const double MutationForce = 0.5;

    private static readonly double[] Population =
    [
        1.0, 2.0, 3.0, 4.0, 5.0,
        10.0, 20.0, 30.0, 40.0, 50.0,
        3.0, 3.0, 3.0, 3.0, 3.0,
        1.0, 1.0, 1.0, 1.0, 1.0,
    ];

    private static readonly double[] Parent = [10.0, 20.0, 30.0, 40.0, 50.0];
    private static readonly double[] Mutant = [2.0, 3.0, 4.0, 5.0, 6.0];
    private static readonly double[] Lower = [-100.0, -100.0, -100.0, -100.0, -100.0];
    private static readonly double[] Upper = [100.0, 100.0, 100.0, 100.0, 100.0];

    /// <summary>
    /// CR = 0 scales to a threshold of 0; with every per-gene draw above it, the trial is the parent
    /// except gene <c>jrand</c>, which is the mutant's. One spare draw is scripted, and left unused.
    /// </summary>
    [Fact]
    public void CrossoverProbabilityZeroTakesExactlyTheGuaranteedGene()
    {
        Assert.Equal(0UL, DeStep.CrossoverThreshold(0.0));
        var draws = Script(1UL, ulong.MaxValue, 0x8000_0000_0000_0000UL, 12345UL, 7UL);

        var trial = Build(ref draws, 0.0);

        Assert.Equal([10.0, 20.0, 4.0, 40.0, 50.0], trial);
        Assert.Equal(4, draws.WordsConsumed);
    }

    /// <summary>CR = 1 scales to <see cref="ulong.MaxValue"/>: every gene is the mutant's, even on the largest draw.</summary>
    [Fact]
    public void CrossoverProbabilityOneTakesEveryGene()
    {
        Assert.Equal(ulong.MaxValue, DeStep.CrossoverThreshold(1.0));
        var draws = Script(ulong.MaxValue, 0UL, ulong.MaxValue - 1UL, 1UL);

        var trial = Build(ref draws, 1.0);

        Assert.Equal(Mutant, trial);
        Assert.Equal(4, draws.WordsConsumed);
    }

    /// <summary>
    /// CR = ½, threshold 2⁶³: a scripted mix of draws at, above and below the threshold gives, gene by
    /// gene, the closed form <c>u_j = v_j</c> if <c>draw_j ≤ T</c> or <c>j = jrand</c>, else <c>x_j</c>.
    /// </summary>
    [Fact]
    public void AScriptedMixMatchesTheClosedFormGeneByGene()
    {
        const ulong threshold = 0x8000_0000_0000_0000UL;
        Assert.Equal(threshold, DeStep.CrossoverThreshold(0.5));
        ulong[] words = [threshold, threshold + 1UL, 0UL, ulong.MaxValue];
        var draws = Script(words);

        var trial = Build(ref draws, 0.5);

        var word = 0;
        for (var j = 0; j < Parent.Length; j++)
        {
            var crossed = j == GuaranteedGene || words[word++] <= threshold;
            Assert.Equal(crossed ? Mutant[j] : Parent[j], trial[j]);
        }

        Assert.Equal([2.0, 20.0, 4.0, 5.0, 50.0], trial);
        Assert.Equal(words.Length, draws.WordsConsumed);
        Assert.Equal(4 + words.Length, draws.Consumed);
    }

    private static ScriptedDraws Script(params ulong[] words)
    {
        // Donors for i = 1 out of N − 1 = 3 others: 0 → r1 = 0; 1 → skips i → r2 = 2; 2 → r3 = 3.
        List<ScriptedDraw> script =
        [
            ScriptedDraw.Index(0, 3),
            ScriptedDraw.Index(1, 3),
            ScriptedDraw.Index(2, 3),
            ScriptedDraw.Index(GuaranteedGene, 5),
        ];
        script.AddRange(words.Select(ScriptedDraw.Word));
        return new ScriptedDraws(script);
    }

    private static double[] Build(ref ScriptedDraws draws, double crossoverProbability)
    {
        using var step = new HostStep();
        return step.BuildTrial(ref draws, Individual, MutationForce, crossoverProbability, Population, Lower, Upper);
    }
}
