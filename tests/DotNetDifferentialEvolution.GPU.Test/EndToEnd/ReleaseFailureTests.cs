using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A6 of the GPU package's Kernels ACCEPTANCE.md: a release that throws stops no other. The optimizer, on the CPU
/// accelerator, is given a child whose release throws (<see cref="ReleaseThatThrows"/>, as ILGPU's half-built
/// <c>CudaKernel</c> does) through the builder's internal <c>WithPlantedRelease</c>, first among everything it allocates. A
/// second such child is registered with the accelerator itself, so that the accelerator's own disposal throws, which the lease
/// must survive. (a) <c>Dispose</c> releases everything else and throws the failures together; (b) a <c>Build</c> that fails
/// throws its own exception with the failures in its <c>Data</c>; (c) <c>Dispose</c> from the observer faults the task and
/// the process lives.
/// </summary>
public class ReleaseFailureTests
{
    private const string OwnFailure = "the release in the optimizer's own list";
    private const string AcceleratorFailure = "the release done by the accelerator's disposal";

    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>
    /// A6 (a): <c>Dispose</c> still releases every other buffer and kernel and the owned context, then throws an
    /// <see cref="AggregateException"/> holding both failures; a second <c>Dispose</c> does nothing.
    /// </summary>
    [Fact]
    public void DisposeReleasesEverythingElseThenThrowsTheFailuresTogether()
    {
        using var optimizer = Planted(default(Sphere), observer: null).Build();
        using var stray = new ReleaseThatThrows(optimizer.Lease.Accelerator, AcceleratorFailure);
        var others = optimizer.Allocated.Where(allocated => allocated is not ReleaseThatThrows).ToList();
        var context = optimizer.Lease.Accelerator.Context;
        Assert.NotEmpty(others);

        var failure = Assert.Throws<AggregateException>(optimizer.Dispose);

        Assert.Equal([AcceleratorFailure, OwnFailure], failure.InnerExceptions.Select(inner => inner.Message).Order(StringComparer.Ordinal));
        Assert.All(others, allocated => Assert.True(allocated.IsDisposed, $"{allocated.GetType().Name} was not released."));
        Assert.True(context.IsDisposed, "The owned context was not released.");
        Assert.Null(Record.Exception(optimizer.Dispose));
    }

    /// <summary>
    /// A6 (b): a <c>Build</c> that fails on an objective ILGPU cannot compile throws that compile exception, not the release
    /// failure; the release failure is in its <c>Data</c>.
    /// </summary>
    [Fact]
    public void AFailingBuildThrowsItsOwnExceptionWithTheReleaseFailureInItsData()
    {
        var failure = Assert.ThrowsAny<Exception>(() => Planted(default(UncompilableObjective), observer: null).Build());

        _ = Assert.IsType<InternalCompilerException>(failure);
        var released = Assert.IsType<AggregateException>(failure.Data["DotNetDifferentialEvolution.GPU.ReleaseFailures"]);
        Assert.Equal(OwnFailure, Assert.Single(released.InnerExceptions).Message);
    }

    /// <summary>
    /// A6 (c): <c>Dispose</c> from the observer returns normally; the run's thread releases everything, and the failure
    /// faults the task with an <see cref="AggregateException"/> instead of ending the process.
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ADisposeFromTheObserverFaultsTheTaskWithTheReleaseFailure()
    {
        var observer = new DisposingObserver(disposeAtGeneration: 3);
        using var optimizer = Planted(default(Sphere), observer).Build();
        observer.Optimizer = optimizer;
        var others = optimizer.Allocated.Where(allocated => allocated is not ReleaseThatThrows).ToList();

        var run = optimizer.RunAsync();
        var failure = await Assert.ThrowsAsync<AggregateException>(() => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);

        Assert.True(run.IsFaulted);
        Assert.Equal(OwnFailure, Assert.Single(failure.InnerExceptions).Message);
        Assert.Null(observer.DisposeFailure);
        Assert.All(others, allocated => Assert.True(allocated.IsDisposed, $"{allocated.GetType().Name} was not released."));
    }

    private static GpuBuilder<TFunction> Planted<TFunction>(TFunction function, IGpuPopulationUpdatedHandler? observer)
        where TFunction : struct, IGpuFitnessFunction
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(function)
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(100)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1);
        if (observer is not null)
        {
            _ = stage.WithPopulationUpdateHandler(observer);
        }

        return ((GpuBuilder<TFunction>)stage).WithPlantedRelease(accelerator => new ReleaseThatThrows(accelerator, OwnFailure));
    }

    /// <summary>An objective ILGPU cannot compile: a kernel may not throw.</summary>
    internal readonly struct UncompilableObjective : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes) => throw new InvalidOperationException("An objective may not throw.");
    }

    /// <summary>A child of an accelerator whose release throws, as ILGPU's half-built <c>CudaKernel</c> does.</summary>
    /// <param name="accelerator">The accelerator it is registered with, which disposes it if nothing else does.</param>
    /// <param name="message">The message of the exception its release throws.</param>
    internal sealed class ReleaseThatThrows(Accelerator accelerator, string message) : AcceleratorObject(accelerator)
    {
        /// <inheritdoc />
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
