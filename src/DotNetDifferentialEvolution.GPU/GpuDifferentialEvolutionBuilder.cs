using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The entry point of the package: a staged builder, like the CPU package's, that carries the
/// objective's type through every stage so the kernels are compiled for it.
/// </summary>
public static class GpuDifferentialEvolutionBuilder
{
    /// <summary>Starts a run for <paramref name="function"/>.</summary>
    /// <typeparam name="TFunction">The objective, a struct compiled into the kernel.</typeparam>
    /// <param name="function">The objective, with any data it carries.</param>
    /// <returns>The first stage.</returns>
    public static IGpuBoundsRequired<TFunction> ForFunction<TFunction>(TFunction function)
        where TFunction : struct, IGpuFitnessFunction =>
        new GpuBuilder<TFunction>(
            function,
            null,
            (accelerator, objective, rule, _) => new KernelLauncher<TFunction>(accelerator, objective, rule));
}
