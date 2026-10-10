using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A11 of the GPU package's Kernels ACCEPTANCE.md: after <c>Build</c> and <c>Dispose</c> on one thread, that thread's
/// <see cref="Accelerator.Current"/> is what it was before <c>Build</c>. ILGPU binds every accelerator it creates to the
/// creating thread, and only disposing the bound accelerator takes a thread's binding back, so every case runs on a thread
/// of its own, whose binding starts as the test sets it, and looks after <c>Build</c> as well as after <c>Dispose</c>.
/// </summary>
public class ThreadBindingTests
{
    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>A11: a thread bound to nothing is bound to nothing after <c>Build</c> and after <c>Dispose</c>.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task AThreadBoundToNothingIsBoundToNothingAfterwards() => OnAThreadOfItsOwn(() =>
    {
        Assert.Null(Accelerator.Current);

        using var optimizer = Stage().OnDevice(GpuDevice.Cpu).WithSeed(1).Build();
        Assert.Null(Accelerator.Current);

        optimizer.Dispose();
        Assert.Null(Accelerator.Current);
    });

    /// <summary>A11: a thread bound to the caller's accelerator is bound to it still, after <c>Build</c> on a device of its own and after <c>Dispose</c>.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task AThreadBoundToTheCallersAcceleratorIsBoundToItAfterwards() => OnAThreadOfItsOwn(() =>
    {
        using var context = Context.Create(builder => builder.CPU());
        using var callers = context.CreateCPUAccelerator(0);
        Assert.Same(callers, Accelerator.Current);

        using var optimizer = Stage().OnDevice(GpuDevice.Cpu).WithSeed(1).Build();
        Assert.Same(callers, Accelerator.Current);

        optimizer.Dispose();
        Assert.Same(callers, Accelerator.Current);
    });

    /// <summary>A11: a run on the caller's accelerator leaves the thread bound to the accelerator it was bound to.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task AThreadBoundToAnotherAcceleratorStaysBoundToItWhenTheCallersIsUsed() => OnAThreadOfItsOwn(() =>
    {
        using var context = Context.Create(builder => builder.CPU());
        using var other = context.CreateCPUAccelerator(0);
        using var callers = context.CreateCPUAccelerator(0);
        other.Bind();
        Assert.Same(other, Accelerator.Current);

        using var optimizer = Stage().OnAccelerator(callers).WithSeed(1).Build();
        Assert.Same(other, Accelerator.Current);

        optimizer.Dispose();
        Assert.Same(other, Accelerator.Current);
    });

    /// <summary>A11: a <c>Build</c> that fails leaves the thread bound to nothing.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task AFailingBuildLeavesTheThreadBoundToNothing() => OnAThreadOfItsOwn(() =>
    {
        Assert.Null(Accelerator.Current);

        _ = Assert.ThrowsAny<Exception>(() => GpuDifferentialEvolutionBuilder.ForFunction(default(ReleaseFailureTests.UncompilableObjective))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(5)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1)
            .Build());

        Assert.Null(Accelerator.Current);
    });

    private static Task OnAThreadOfItsOwn(Action body) =>
        Task.Factory.StartNew(body, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static IGpuDeviceRequired<Sphere> Stage() =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(5);
}
