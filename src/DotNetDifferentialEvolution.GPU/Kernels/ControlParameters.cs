using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// A trial's F and CR under each <see cref="ParameterRule"/>, drawn as the CPU package's control-parameter providers draw
/// them, in the same order and with the same formulas, so the same draws give the same F and CR bit for bit
/// (ACCEPTANCE.md, S4): <c>JdeStrategy</c>, <c>JadeStrategy</c>, <c>ShadeStrategy</c>, and
/// <c>RandomDistributionHelper</c>'s two-uniform Gaussian and its Cauchy.
/// </summary>
internal static class ControlParameters
{
    /// <summary>jDE's τ1, the probability that F is regenerated (<c>JdeStrategy.DefaultFAdaptationProbability</c>).</summary>
    public const double JdeFAdaptationProbability = 0.1;

    /// <summary>jDE's τ2, the probability that CR is regenerated (<c>JdeStrategy.DefaultCrAdaptationProbability</c>).</summary>
    public const double JdeCrAdaptationProbability = 0.1;

    /// <summary>jDE's F_l, the smallest regenerated F (<c>JdeStrategy.DefaultMinMutationForce</c>).</summary>
    public const double JdeMinMutationForce = 0.1;

    /// <summary>jDE's F_u, the width of the regenerated F (<c>JdeStrategy.DefaultMutationForceRange</c>).</summary>
    public const double JdeMutationForceRange = 0.9;

    /// <summary>The standard deviation of CR around its mean or memory slot, under JADE and SHADE.</summary>
    public const double CrStandardDeviation = 0.1;

    /// <summary>The scale of F's Cauchy around its mean or memory slot, under JADE and SHADE.</summary>
    public const double FScale = 0.1;

    /// <summary>
    /// jDE (<c>JdeStrategy.GetControlParameters</c>): with probability τ1, <c>F = F_l + u·F_u</c>, else the individual's F;
    /// then with probability τ2, <c>CR = u</c>, else the individual's CR. Each decision draw comes first.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="currentMutationForce">The individual's F.</param>
    /// <param name="currentCrossoverProbability">The individual's CR.</param>
    /// <param name="mutationForce">The trial's F.</param>
    /// <param name="crossoverProbability">The trial's CR.</param>
    public static void Jde<TDraws>(
        ref TDraws draws,
        double currentMutationForce,
        double currentCrossoverProbability,
        out double mutationForce,
        out double crossoverProbability)
        where TDraws : struct, IDrawSource
    {
        mutationForce = draws.NextUnitDouble() < JdeFAdaptationProbability
            ? JdeMinMutationForce + draws.NextUnitDouble() * JdeMutationForceRange
            : currentMutationForce;
        crossoverProbability = draws.NextUnitDouble() < JdeCrAdaptationProbability
            ? draws.NextUnitDouble()
            : currentCrossoverProbability;
    }

    /// <summary>
    /// JADE (<c>JadeStrategy.GetControlParameters</c>): <c>CR = clamp(N(μCR, 0.1), 0, 1)</c>; <c>F</c> from
    /// <c>Cauchy(μF, 0.1)</c>, redrawn while at most 0 and cut to 1 above it.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="meanCrossoverProbability">μCR.</param>
    /// <param name="meanMutationForce">μF.</param>
    /// <param name="mutationForce">The trial's F.</param>
    /// <param name="crossoverProbability">The trial's CR.</param>
    public static void Jade<TDraws>(
        ref TDraws draws,
        double meanCrossoverProbability,
        double meanMutationForce,
        out double mutationForce,
        out double crossoverProbability)
        where TDraws : struct, IDrawSource
    {
        crossoverProbability = ClampToUnit(Gaussian(ref draws, meanCrossoverProbability, CrStandardDeviation));
        mutationForce = PositiveCauchyUpToOne(ref draws, meanMutationForce);
    }

    /// <summary>
    /// SHADE and L-SHADE (<c>ShadeStrategy.GetControlParameters</c>): a slot r uniform in [0, H); <c>CR = 0</c> when
    /// <c>M_CR[r]</c> is negative (terminal, no draw), else <c>clamp(N(M_CR[r], 0.1), 0, 1)</c>; F as JADE's around
    /// <c>M_F[r]</c>.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="memory">The H CR slots, then the H F slots.</param>
    /// <param name="memorySize">H.</param>
    /// <param name="mutationForce">The trial's F.</param>
    /// <param name="crossoverProbability">The trial's CR.</param>
    public static void Shade<TDraws>(
        ref TDraws draws,
        ArrayView<double> memory,
        int memorySize,
        out double mutationForce,
        out double crossoverProbability)
        where TDraws : struct, IDrawSource
    {
        var slot = draws.NextIndex(memorySize);
        var memoryCrossoverProbability = memory[slot];
        crossoverProbability = memoryCrossoverProbability < 0.0
            ? 0.0
            : ClampToUnit(Gaussian(ref draws, memoryCrossoverProbability, CrStandardDeviation));
        mutationForce = PositiveCauchyUpToOne(ref draws, memory[memorySize + slot]);
    }

    /// <summary>
    /// The CPU package's <c>RandomDistributionHelper.NextGaussian</c> for a provider other than
    /// <c>SeededRandomProvider</c>: <c>u1 = 1 − u</c>, <c>u2 = 1 − u</c>,
    /// <c>mean + sd·√(−2 ln u1)·cos(2π u2)</c>; the sine half is discarded.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="mean">The mean.</param>
    /// <param name="standardDeviation">The standard deviation.</param>
    /// <returns>The sample.</returns>
    public static double Gaussian<TDraws>(ref TDraws draws, double mean, double standardDeviation)
        where TDraws : struct, IDrawSource
    {
        // Guard against log(0), as the CPU package does.
        var u1 = 1.0 - draws.NextUnitDouble();
        var u2 = 1.0 - draws.NextUnitDouble();

        var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + standardDeviation * standardNormal;
    }

    /// <summary>The CPU package's <c>RandomDistributionHelper.NextCauchy</c>: <c>location + scale·tan(π(u − 0.5))</c>.</summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="location">The location.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The sample.</returns>
    public static double Cauchy<TDraws>(ref TDraws draws, double location, double scale)
        where TDraws : struct, IDrawSource
    {
        var u = draws.NextUnitDouble();
        return location + scale * Math.Tan(Math.PI * (u - 0.5));
    }

    /// <summary><c>Math.Clamp(value, 0, 1)</c>, as the CPU strategies clamp CR: <see cref="double.NaN"/> stays <see cref="double.NaN"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value in [0, 1].</returns>
    public static double ClampToUnit(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;

    private static double PositiveCauchyUpToOne<TDraws>(ref TDraws draws, double location)
        where TDraws : struct, IDrawSource
    {
        double mutationForce;
        do
        {
            mutationForce = Cauchy(ref draws, location, FScale);
        }
        while (mutationForce <= 0.0);

        return mutationForce > 1.0 ? 1.0 : mutationForce;
    }
}
