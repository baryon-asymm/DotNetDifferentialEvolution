using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The last stage of the builder: the optional settings, then <see cref="Build"/>.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuDifferentialEvolutionBuilder<TFunction>
    where TFunction : struct, IGpuFitnessFunction
{
    /// <summary>Makes the run reproducible: the same seed, device and package and ILGPU versions give a bit-identical result.</summary>
    /// <param name="seed">The seed, the Philox key.</param>
    /// <returns>This stage.</returns>
    IGpuDifferentialEvolutionBuilder<TFunction> WithSeed(int seed);

    /// <summary>Observes the population every <paramref name="everyNGenerations"/> generations, on the run's thread.</summary>
    /// <param name="handler">The observer.</param>
    /// <param name="everyNGenerations">The period; at least 1.</param>
    /// <returns>This stage.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="everyNGenerations"/> is below 1.</exception>
    IGpuDifferentialEvolutionBuilder<TFunction> WithPopulationUpdateHandler(IGpuPopulationUpdatedHandler handler, int everyNGenerations = 1);

    /// <summary>
    /// Opens the device, compiles the kernels, samples the initial population on the device and
    /// evaluates it: costs N evaluations and the compile time.
    /// </summary>
    /// <returns>The optimizer, ready to run.</returns>
    /// <exception cref="InvalidOperationException">An explicit device cannot be opened; the message names it.</exception>
    GpuDifferentialEvolution Build();
}
