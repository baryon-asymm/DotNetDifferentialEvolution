namespace DotNetDifferentialEvolution.GPU;

/// <summary>The fourth stage of the builder: when the run stops.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuTerminationConditionRequired<TFunction>
    where TFunction : struct
{
    /// <summary>Runs exactly <paramref name="maxGenerations"/> generations.</summary>
    /// <param name="maxGenerations">The number of generations; at least 1.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGenerations"/> is below 1.</exception>
    IGpuDeviceRequired<TFunction> WithGenerationLimit(int maxGenerations);

    /// <summary>
    /// Stops at the first generation boundary where the evaluation count, which starts at N, is at
    /// least <paramref name="maxEvaluations"/>; at least one generation runs.
    /// </summary>
    /// <param name="maxEvaluations">The evaluation budget; at least 1.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is below 1.</exception>
    IGpuDeviceRequired<TFunction> WithEvaluationLimit(long maxEvaluations);

    /// <summary>
    /// Stops when the best fitness has not moved by more than <paramref name="stagnationThreshold"/> for
    /// <paramref name="maxStagnationStreak"/> generations: the CPU package's <c>StagnationStreakTerminationStrategy</c>.
    /// After each generation, when <c>|best − last| &gt; threshold</c>, <c>last = best</c> and the streak restarts at 0;
    /// otherwise it grows. <c>last</c> starts at <see cref="double.MinValue"/>. The rule runs on the device.
    /// </summary>
    /// <param name="maxStagnationStreak">The streak at which the run stops; at least 1.</param>
    /// <param name="stagnationThreshold">The smallest change that counts as progress; finite and not negative.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The streak is below 1, or the threshold is negative or not finite.</exception>
    IGpuDeviceRequired<TFunction> WithStagnationLimit(int maxStagnationStreak, double stagnationThreshold);
}
