using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The fourth stage of the builder: when the run stops.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuTerminationConditionRequired<TFunction>
    where TFunction : struct, IGpuFitnessFunction
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
}
