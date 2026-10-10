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
            (accelerator, objective, rule, populationSize) => new KernelLauncher<TFunction>(accelerator, objective, populationSize, rule));

    /// <summary>
    /// Starts a run for a pointwise <paramref name="function"/>: an objective of <paramref name="pointCount"/> independent
    /// parts, evaluated part by part in <c>N·P</c> threads and combined per individual. Every later stage is the same as
    /// after <see cref="ForFunction{TFunction}"/>.
    /// </summary>
    /// <typeparam name="TFunction">The objective, a struct compiled into the kernel.</typeparam>
    /// <typeparam name="TPoint">The result of one point, an unmanaged struct.</typeparam>
    /// <param name="function">The objective, with any data it carries.</param>
    /// <param name="pointCount"><c>P</c>, the number of points of an individual; at least 1.</param>
    /// <returns>The first stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pointCount"/> is below 1.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TPoint"/> is not of sequential layout, has a field that is not a <see cref="byte"/>,
    /// <see cref="sbyte"/>, <see cref="short"/>, <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>,
    /// <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/> or <see cref="double"/>, an enum of them or a struct
    /// that follows the same rule, or is packed or padded so that its size is not its natural one.
    /// </exception>
    public static IGpuBoundsRequired<TFunction> ForPointwiseFunction<TFunction, TPoint>(TFunction function, int pointCount)
        where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
        where TPoint : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pointCount, 1);
        PointLayout.Require<TPoint>();
        return new GpuBuilder<TFunction>(
            function,
            pointCount,
            (accelerator, objective, rule, populationSize) =>
                new PointwiseKernelLauncher<TFunction, TPoint>(accelerator, objective, pointCount, populationSize, rule));
    }
}
