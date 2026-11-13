using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The parameter adaptation of JADE and SHADE after a generation, as the CPU package's <c>JadeStrategy</c> and
/// <c>ShadeStrategy</c> (with <c>LShadeStrategy</c>'s terminal CR and Lehmer CR mean) compute it from the improved trials
/// (ACCEPTANCE.md, S7). Pure functions, called by the device kernels and by the tests on the host.
/// </summary>
internal static class AdaptationRules
{
    private const double MinNormal = 2.2250738585072014e-308; // 2^-1022
    private const double SignificandUnit = 4503599627370496.0; // 2^52
    private const double TinyWeightLimit = MinNormal * SignificandUnit;

    /// <summary>The CR value of a terminal SHADE memory slot (<c>ShadeStrategy.TerminalCrValue</c>).</summary>
    public const double TerminalCr = -1.0;

    /// <summary>The initial JADE means and SHADE memory values (<c>JadeStrategy.DefaultInitialMean</c>, <c>ShadeStrategy.DefaultInitialMemoryValue</c>).</summary>
    public const double InitialValue = 0.5;

    /// <summary>
    /// The weight a trial enters the sums with: under JADE 1 for every improved trial; under SHADE its improvement
    /// <c>f(parent) − f(trial)</c> for finite endpoints. Positive infinity signals an overflowing
    /// difference from finite endpoints. A trial that did not improve has no weight.
    /// </summary>
    /// <param name="rule">JADE or SHADE.</param>
    /// <param name="outcome">The trial's outcome.</param>
    /// <param name="parentFitness">f(parent).</param>
    /// <param name="trialFitness">f(trial).</param>
    /// <returns>The weight, or <see cref="double.NaN"/> when the trial does not enter the sums.</returns>
    public static double WeightOf(ParameterRule rule, int outcome, double parentFitness, double trialFitness)
    {
        if (outcome != Selection.Improved || (rule == ParameterRule.Shade && (!double.IsFinite(parentFitness) || !double.IsFinite(trialFitness))))
        {
            return double.NaN;
        }

        var difference = parentFitness - trialFitness;
        return rule == ParameterRule.Shade ? difference : 1.0;
    }

    /// <summary>
    /// Selects the generation's common weight scale. A zero maximum can also mean the device
    /// flushed tiny differences to zero in the maximum pass. Recompute those in normal units too.
    /// </summary>
    /// <param name="largestWeight">The generation's largest weight, 0 when there is none.</param>
    /// <param name="count">N, the active population size.</param>
    /// <returns>The divisor of every weight of the generation.</returns>
    public static double ScaleOf(double largestWeight, int count) => largestWeight switch
    {
        < TinyWeightLimit => MinNormal,
        _ when largestWeight > double.MaxValue / (2.0 * count) => largestWeight,
        _ => 1.0,
    };

    /// <summary>Scales a usable weight before multiplying by F or CR, using the endpoints when the difference exceeds double range.</summary>
    /// <param name="weight">The value from <see cref="WeightOf"/>.</param>
    /// <param name="scale">The generation's scale, or 1 for JADE.</param>
    /// <param name="parentFitness">The parent's fitness.</param>
    /// <param name="trialFitness">The trial's fitness.</param>
    /// <returns>The weight in the generation's common units.</returns>
    public static double ScaledWeight(double weight, double scale, double parentFitness, double trialFitness)
    {
        if (scale == MinNormal)
        {
            // Equal large endpoints have zero difference but could overflow the conversion individually.
            return BitConverter.DoubleToInt64Bits(parentFitness) == BitConverter.DoubleToInt64Bits(trialFitness)
                ? 0.0
                : InMinNormalUnits(parentFitness) - InMinNormalUnits(trialFitness);
        }

        return scale switch
        {
            double.PositiveInfinity => (0.5 * parentFitness - 0.5 * trialFitness) / (0.5 * double.MaxValue),
            1.0 => weight,
            _ => weight / scale,
        };
    }

    // A subnormal has no implicit leading one. Its signed significand represents its exact
    // value in units of 2^-1074; the conversion below never needs floating-point denormals.
    private static double InMinNormalUnits(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var magnitude = bits & long.MaxValue;
        return magnitude < (1L << 52)
            ? (bits < 0 ? -magnitude : magnitude) / SignificandUnit
            : value * (1.0 / MinNormal);
    }

    internal static ExtendedMoments.Binary ExtendedWeight(double parent, double trial)
    {
        var difference = parent - trial;
        if (difference == double.PositiveInfinity)
        {
            return ExtendedMoments.Binary.From(0.5 * parent - 0.5 * trial).Shift(1);
        }

        // Near the normal boundary a device may flush either input or the difference.
        if (difference < TinyWeightLimit && BitConverter.DoubleToInt64Bits(parent) != BitConverter.DoubleToInt64Bits(trial))
        {
            var units = InMinNormalUnits(parent) - InMinNormalUnits(trial);
            return units > 0 ? ExtendedMoments.Binary.From(units).Shift(-1022) : default;
        }

        return difference > 0 ? ExtendedMoments.Binary.From(difference) : default;
    }

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

        var isTerminal = lShade && (slotCr < 0.0 || BitConverter.DoubleToInt64Bits(sums.MaxCr) <= 0);
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
