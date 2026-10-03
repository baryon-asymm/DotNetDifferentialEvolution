using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The third stage of the builder: the scheme, DE/rand/1/bin, and its two parameters.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuMutationStrategyRequired<TFunction>
    where TFunction : struct, IGpuFitnessFunction
{
    /// <summary>Uses DE/rand/1/bin, named as in the CPU builder.</summary>
    /// <param name="mutationForce">F; finite and greater than 0.</param>
    /// <param name="crossoverProbability">CR; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">F is not finite or not positive, or CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability);
}
