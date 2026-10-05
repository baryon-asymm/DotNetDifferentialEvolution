namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// L-SHADE's linear population size reduction, computed on the host from the evaluation count, which the host knows
/// exactly: <c>LShadeStrategy.ComputePlannedPopulationSize</c> clamped as its <c>ReducePopulationSize</c> clamps it
/// (ACCEPTANCE.md, S11).
/// </summary>
internal static class LShadeSchedule
{
    /// <summary>The population L-SHADE ends with (<c>LShadeStrategy.MinimumPopulationSize</c>).</summary>
    public const int MinimumPopulationSize = 4;

    /// <summary>
    /// The population size after a generation: <c>round((N_min − N_init)·min(1, nfe / budget) + N_init)</c>, half
    /// away from zero, clamped to [N_min, the current size].
    /// </summary>
    /// <param name="initialPopulationSize">N_init.</param>
    /// <param name="maxEvaluationNumber">The budget.</param>
    /// <param name="evaluationCount">The evaluations so far, the generation just run included.</param>
    /// <param name="currentPopulationSize">The current N.</param>
    /// <returns>The next N.</returns>
    public static int NextPopulationSize(int initialPopulationSize, long maxEvaluationNumber, long evaluationCount, int currentPopulationSize)
    {
        var progress = Math.Min(1.0, (double)evaluationCount / maxEvaluationNumber);
        var planned = (int)Math.Round(
            (MinimumPopulationSize - initialPopulationSize) * progress + initialPopulationSize,
            MidpointRounding.AwayFromZero);
        return Math.Clamp(planned, MinimumPopulationSize, currentPopulationSize);
    }
}
