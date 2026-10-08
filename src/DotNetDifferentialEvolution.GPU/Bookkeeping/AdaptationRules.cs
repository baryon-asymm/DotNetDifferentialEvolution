using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The parameter adaptation of JADE and SHADE after a generation, as the CPU package's <c>JadeStrategy</c> and
/// <c>ShadeStrategy</c> (with <c>LShadeStrategy</c>'s terminal CR and Lehmer CR mean) compute it from the improved trials
/// (ACCEPTANCE.md, S7). Pure functions, called by the device kernels and by the tests on the host.
/// </summary>
internal static class AdaptationRules
{
    /// <summary>The CR value of a terminal SHADE memory slot (<c>ShadeStrategy.TerminalCrValue</c>).</summary>
    public const double TerminalCr = -1.0;

    /// <summary>The initial JADE means and SHADE memory values (<c>JadeStrategy.DefaultInitialMean</c>, <c>ShadeStrategy.DefaultInitialMemoryValue</c>).</summary>
    public const double InitialValue = 0.5;

    /// <summary>
    /// The weight a trial enters the sums with: under JADE 1 for every improved trial; under SHADE its improvement
    /// <c>f(parent) − f(trial)</c>, and none (<see cref="double.NaN"/>) when that is not finite, as
    /// <c>ShadeStrategy</c> skips it. A trial that did not improve has no weight.
    /// </summary>
    /// <param name="rule">JADE or SHADE.</param>
    /// <param name="outcome">The trial's outcome.</param>
    /// <param name="parentFitness">f(parent).</param>
    /// <param name="trialFitness">f(trial).</param>
    /// <returns>The weight, or <see cref="double.NaN"/> when the trial does not enter the sums.</returns>
    public static double WeightOf(ParameterRule rule, int outcome, double parentFitness, double trialFitness)
    {
        if (outcome != Selection.Improved)
        {
            return double.NaN;
        }

        if (rule != ParameterRule.Shade)
        {
            return 1.0;
        }

        var weight = parentFitness - trialFitness;
        return double.IsFinite(weight) ? weight : double.NaN;
    }

    /// <summary>
    /// The divisor of SHADE's weights, so that no sum of N of them overflows (<c>ShadeStrategy</c> applies the same rule):
    /// the generation's largest weight when it exceeds <c>double.MaxValue / (2·N)</c>, else 1.0, which changes no bit.
    /// With F, CR ≤ 1 no weighted sum then exceeds N, and the means do not depend on the scale.
    /// </summary>
    /// <param name="largestWeight">The generation's largest weight, 0 when there is none.</param>
    /// <param name="count">N, the active population size.</param>
    /// <returns>The divisor of every weight of the generation.</returns>
    public static double ScaleOf(double largestWeight, int count) =>
        largestWeight > double.MaxValue / (2.0 * count) ? largestWeight : 1.0;

    /// <summary>
    /// JADE's update (<c>JadeStrategy.AdaptParameterMeans</c>): with no improved trial nothing changes; else
    /// <c>μCR = (1 − c)·μCR + c·mean(S_CR)</c>, and when ΣF &gt; 0, <c>μF = (1 − c)·μF + c·ΣF²/ΣF</c>.
    /// </summary>
    /// <param name="sums">The sums, with weight 1 per improved trial.</param>
    /// <param name="adaptationRate">c.</param>
    /// <param name="meanCr">μCR.</param>
    /// <param name="meanF">μF.</param>
    public static void UpdateJadeMeans(SuccessSums sums, double adaptationRate, ref double meanCr, ref double meanF)
    {
        if (sums.Weight == 0.0)
        {
            return;
        }

        meanCr = (1.0 - adaptationRate) * meanCr + adaptationRate * (sums.WeightedCr / sums.Weight);
        if (sums.WeightedF > 0.0)
        {
            meanF = (1.0 - adaptationRate) * meanF + adaptationRate * (sums.WeightedFSquared / sums.WeightedF);
        }
    }

    /// <summary>
    /// SHADE's update of the slot at the memory index (<c>ShadeStrategy.UpdateMemory</c>): nothing when Σw ≤ 0, and the
    /// index does not advance; else the slot's CR becomes the weighted mean (L-SHADE: the weighted Lehmer mean when
    /// Σw·CR &gt; 0, and the terminal value when the slot is terminal or every successful CR is 0), its F the weighted
    /// Lehmer mean when Σw·F &gt; 0, and the index advances.
    /// </summary>
    /// <param name="sums">The weighted sums.</param>
    /// <param name="lShade">Whether L-SHADE's terminal CR and Lehmer CR mean apply.</param>
    /// <param name="slotCr">The slot's CR, updated.</param>
    /// <param name="slotF">The slot's F, updated.</param>
    /// <returns>Whether the slot was updated, and so whether the memory index advances.</returns>
    public static bool UpdateShadeSlot(SuccessSums sums, bool lShade, ref double slotCr, ref double slotF)
    {
        if (sums.Weight <= 0.0)
        {
            return false;
        }

        var isTerminal = lShade && (slotCr < 0.0 || sums.MaxCr <= 0.0);
        var crMean = lShade && sums.WeightedCr > 0.0
            ? sums.WeightedCrSquared / sums.WeightedCr
            : sums.WeightedCr / sums.Weight;
        slotCr = isTerminal ? TerminalCr : crMean;
        if (sums.WeightedF > 0.0)
        {
            slotF = sums.WeightedFSquared / sums.WeightedF;
        }

        return true;
    }
}
