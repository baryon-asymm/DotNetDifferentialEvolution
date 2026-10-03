using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The second stage of the builder: N.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuPopulationSizeRequired<TFunction>
    where TFunction : struct, IGpuFitnessFunction
{
    /// <summary>Sets N, one GPU thread per individual.</summary>
    /// <param name="populationSize">N; at least 4, since DE/rand/1 needs four distinct individuals, and N·D at most <see cref="int.MaxValue"/>.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="populationSize"/> is below 4, or N·D exceeds <see cref="int.MaxValue"/>.</exception>
    IGpuMutationStrategyRequired<TFunction> WithPopulationSize(int populationSize);
}
