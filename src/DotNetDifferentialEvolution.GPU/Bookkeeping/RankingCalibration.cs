namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The rule that picks the ranking limit L of one run: the largest N ranked by counting, the bitonic network above it.
/// The crossover of the two is the device's (between 4 096 and 8 192 on an RTX 5070 Ti, between 1 024 and 2 048 on a
/// gfx1036), so the constructor of <see cref="GenerationBookkeeping"/> times both on the device and asks this rule
/// (HISTORY.md, ranking-calibrated-2026-10-10, decision 2; check A3). The order is the same whichever ranks (S9), so L
/// changes speed, never a result.
/// </summary>
internal static class RankingCalibration
{
    /// <summary>L for an initial population of at most this size, which is never timed; and L when counting is slower at the first size timed.</summary>
    public const int Floor = 1024;

    /// <summary>The largest population size timed.</summary>
    public const int Ceiling = 8192;

    /// <summary>L on the CPU accelerator, which is not timed: its time is the host's thread pool.</summary>
    public const int UntimedLimit = 2048;

    /// <summary>The first population size timed; the next sizes double up to <see cref="Ceiling"/>.</summary>
    private const int FirstSize = 2048;

    /// <summary>
    /// Asks <paramref name="time"/> at n = 2 048, 4 096 and 8 192, each capped at <paramref name="populationSize"/>, in
    /// ascending order and distinct, and takes the last n at which counting is not slower, stopping at the first n at
    /// which it is.
    /// </summary>
    /// <param name="populationSize">N_init.</param>
    /// <param name="time">The two rankings' time at a population size; never asked when <paramref name="populationSize"/> is at most <see cref="Floor"/>.</param>
    /// <returns><see cref="Floor"/> when N_init is at most it or counting is slower at the first n; else the last n at which it is not slower.</returns>
    public static int LimitOf(int populationSize, Func<int, RankingTimes> time)
    {
        ArgumentNullException.ThrowIfNull(time);
        if (populationSize <= Floor)
        {
            return Floor;
        }

        var limit = Floor;
        var previous = 0;
        for (var size = FirstSize; size <= Ceiling; size *= 2)
        {
            var n = Math.Min(size, populationSize);
            if (n == previous)
            {
                continue;
            }

            var times = time(n);
            if (!(times.Counting <= times.Bitonic))
            {
                break;
            }

            limit = n;
            previous = n;
        }

        return limit;
    }
}
