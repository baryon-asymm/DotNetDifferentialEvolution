namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The CPU package's <c>StagnationStreakTerminationStrategy.ShouldTerminate</c> after a generation (ACCEPTANCE.md, S12):
/// when <c>|best − last| &gt; threshold</c>, <c>last = best</c> and the streak restarts at 0; otherwise the streak grows;
/// the run stops when the streak reaches the limit. <c>last</c> starts at <see cref="double.MinValue"/>.
/// </summary>
internal static class StagnationRule
{
    /// <summary>The value <c>last</c> starts at (<c>StagnationStreakTerminationStrategy.LastBestFitnessFunctionValue</c>).</summary>
    public const double InitialLastBest = double.MinValue;

    /// <summary>Applies the rule to the best value of one generation.</summary>
    /// <param name="best">The best fitness of the generation.</param>
    /// <param name="threshold">The stagnation threshold.</param>
    /// <param name="maxStagnationStreak">The streak at which the run stops.</param>
    /// <param name="lastBest">The last best value that counted as progress; updated.</param>
    /// <param name="streak">The generations since; updated.</param>
    /// <returns>Whether the run stops.</returns>
    public static bool Apply(double best, double threshold, int maxStagnationStreak, ref double lastBest, ref int streak)
    {
        var difference = Math.Abs(best - lastBest);
        if (difference > threshold)
        {
            lastBest = best;
            streak = 0;
        }
        else
        {
            streak++;
        }

        return streak >= maxStagnationStreak;
    }
}
