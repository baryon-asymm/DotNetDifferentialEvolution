using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Check A15 of the GPU package's Kernels ACCEPTANCE.md, the hint: when loading the kernels in <c>Build</c> fails with a
/// <see cref="TypeLoadException"/> and the objective's type (or <c>TPoint</c>) is not visible to ILGPU's runtime assembly,
/// <c>Build</c> throws an <see cref="InvalidOperationException"/> that names the type and both remedies, with ILGPU's exception as
/// its inner exception and the failures of the releases in its <c>Data</c> (A6). A visible type's <see cref="TypeLoadException"/>
/// and every other exception propagate unchanged. ILGPU's CPU accelerator refuses a private nested objective as CUDA does, with
/// an <see cref="InternalCompilerException"/> holding a <see cref="TypeLoadException"/> (measured 2026-10-11), so the first cases
/// run on a real refusal; the others plant the load failure through <c>GpuBuilder.WithLauncherFactory</c>.
/// </summary>
public class InvisibleObjectiveTests
{
    private const string ReleaseFailures = "DotNetDifferentialEvolution.GPU.ReleaseFailures";

    private static readonly double[] Lower = [-1.0, -1.0];
    private static readonly double[] Upper = [1.0, 1.0];

    /// <summary>
    /// A private nested objective, which ILGPU's CPU accelerator cannot load, makes <c>Build</c> throw the hint, with ILGPU's
    /// <see cref="InternalCompilerException"/> and its <see cref="TypeLoadException"/> beneath.
    /// </summary>
    [Fact]
    public void APrivateNestedObjectiveIsRefusedWithTheTypeAndTheRemedies()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => SingleKernel(default(PrivateSphere)).Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(PrivateSphere));
        var ilgpu = Assert.IsType<InternalCompilerException>(failure.InnerException);
        _ = Assert.IsType<TypeLoadException>(ilgpu.InnerException);
    }

    /// <summary>A private nested pointwise objective over a private nested point type is refused, naming the objective.</summary>
    [Fact]
    public void APrivateNestedPointwiseObjectiveIsRefusedNamingTheObjective()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForPointwiseFunction<PrivatePointwise, PrivateResidual>(default, 3);

        var failure = Assert.Throws<InvalidOperationException>(() => OnCpu(stage).Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(PrivatePointwise));
        Assert.DoesNotContain(typeof(PrivateResidual).FullName!, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A load that throws <see cref="TypeLoadException"/> for an invisible objective becomes the hint; the exception is the inner one.</summary>
    [Fact]
    public void ATypeLoadFailureOnAnInvisibleObjectiveBecomesTheHint()
    {
        var planted = new TypeLoadException("planted");

        var failure = Assert.Throws<InvalidOperationException>(
            () => SingleKernel(default(PrivateSphere)).WithLauncherFactory((_, _, _, _) => throw planted).Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(PrivateSphere));
        Assert.Same(planted, failure.InnerException);
    }

    /// <summary>A type that is invisible only by its generic argument is named whole, and the part ILGPU cannot see is named too.</summary>
    [Fact]
    public void AnObjectiveInvisibleByAGenericArgumentNamesTheArgument()
    {
        var planted = new TypeLoadException("planted");

        var failure = Assert.Throws<InvalidOperationException>(
            () => SingleKernel(default(Wrapping<PrivateSphere>)).WithLauncherFactory((_, _, _, _) => throw planted).Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(Wrapping<PrivateSphere>));
        Assert.Contains(typeof(PrivateSphere).FullName!, failure.Message, StringComparison.Ordinal);
        Assert.Same(planted, failure.InnerException);
    }

    /// <summary>A visible objective's <see cref="TypeLoadException"/> propagates as it is.</summary>
    [Fact]
    public void ATypeLoadFailureOnAVisibleObjectiveIsNotWrapped()
    {
        var planted = new TypeLoadException("planted");

        var failure = Assert.Throws<TypeLoadException>(
            () => SingleKernel(default(Sphere)).WithLauncherFactory((_, _, _, _) => throw planted).Build());

        Assert.Same(planted, failure);
    }

    /// <summary>Any other exception from the load of an invisible objective propagates as it is.</summary>
    [Fact]
    public void AnotherFailureOnAnInvisibleObjectiveIsNotWrapped()
    {
        var planted = new NotSupportedException("planted");

        var failure = Assert.Throws<NotSupportedException>(
            () => SingleKernel(default(PrivateSphere)).WithLauncherFactory((_, _, _, _) => throw planted).Build());

        Assert.Same(planted, failure);
    }

    /// <summary>A type-load failure inside another exception is the hint too, as ILGPU reports it.</summary>
    [Fact]
    public void ATypeLoadFailureBeneathAnotherExceptionOnAnInvisibleObjectiveBecomesTheHint()
    {
        var planted = new InvalidOperationException("outer", new TypeLoadException("inner"));

        var failure = Assert.Throws<InvalidOperationException>(
            () => SingleKernel(default(PrivateSphere)).WithLauncherFactory((_, _, _, _) => throw planted).Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(PrivateSphere));
        Assert.Same(planted, failure.InnerException);
    }

    /// <summary>The failures of the releases ride on the hint, as A6 has them ride on ILGPU's exception.</summary>
    [Fact]
    public void TheReleaseFailuresRideOnTheHint()
    {
        var releaseFailure = new NotSupportedException("release");

        var failure = Assert.Throws<InvalidOperationException>(
            () => SingleKernel(default(PrivateSphere))
                .WithLauncherFactory((_, _, _, _) => throw new TypeLoadException("planted"))
                .WithPlantedRelease(_ => new FailingRelease(releaseFailure))
                .Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(PrivateSphere));
        var attached = Assert.IsType<AggregateException>(failure.Data[ReleaseFailures]);
        Assert.Same(releaseFailure, Assert.Single(attached.InnerExceptions));
    }

    /// <summary>A point type that ILGPU cannot see is named when the objective is visible.</summary>
    [Fact]
    public void AnInvisiblePointTypeIsNamedWhenTheObjectiveIsVisible()
    {
        var invisible = DynamicTypes.InternalStruct();
        var planted = new TypeLoadException("planted");

        var failure = Assert.Throws<InvalidOperationException>(() => Pointwise(invisible, planted).Build());

        AssertNamesTheTypeAndTheRemedies(failure, invisible);
        Assert.DoesNotContain(typeof(Sphere).FullName!, failure.Message, StringComparison.Ordinal);
        Assert.Same(planted, failure.InnerException);
    }

    /// <summary>A pointwise objective and a point type that are both visible keep their <see cref="TypeLoadException"/>.</summary>
    [Fact]
    public void ATypeLoadFailureOnAVisiblePointTypeIsNotWrapped()
    {
        var planted = new TypeLoadException("planted");

        var failure = Assert.Throws<TypeLoadException>(() => Pointwise(typeof(double), planted).Build());

        Assert.Same(planted, failure);
    }

    /// <summary>
    /// On CUDA, a private nested objective makes <c>Build</c> throw the hint. Run by hand with the GPU category: CI does not open
    /// a device.
    /// </summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void APrivateNestedObjectiveIsRefusedOnCuda()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => GpuDifferentialEvolutionBuilder.ForFunction(default(PrivateSphere))
                .WithBounds(Lower, Upper)
                .WithPopulationSize(8)
                .WithDefaultMutationStrategy(0.5, 0.5)
                .WithGenerationLimit(1)
                .OnDevice(GpuDevice.Cuda)
                .WithSeed(1)
                .Build());

        AssertNamesTheTypeAndTheRemedies(failure, typeof(PrivateSphere));
    }

    private static void AssertNamesTheTypeAndTheRemedies(InvalidOperationException failure, Type type)
    {
        Assert.Contains(type.FullName!, failure.Message, StringComparison.Ordinal);
        Assert.Contains("Make the type public", failure.Message, StringComparison.Ordinal);
        Assert.Contains("[assembly: InternalsVisibleTo(\"ILGPURuntime\")]", failure.Message, StringComparison.Ordinal);
        Assert.Contains("private or protected nested type", failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
    }

    private static GpuBuilder<TFunction> SingleKernel<TFunction>(TFunction objective)
        where TFunction : struct, IGpuFitnessFunction =>
        OnCpu(GpuDifferentialEvolutionBuilder.ForFunction(objective));

    private static GpuBuilder<TFunction> OnCpu<TFunction>(IGpuBoundsRequired<TFunction> stage)
        where TFunction : struct =>
        (GpuBuilder<TFunction>)stage
            .WithBounds(Lower, Upper)
            .WithPopulationSize(8)
            .WithDefaultMutationStrategy(0.5, 0.5)
            .WithGenerationLimit(1)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1);

    /// <summary>A builder as <c>ForPointwiseFunction</c> makes it, with the point type named by hand and a load that throws.</summary>
    private static GpuBuilder<Sphere> Pointwise(Type pointType, Exception loadFailure) =>
        OnCpu(new GpuBuilder<Sphere>(default, 3, (_, _, _, _) => throw loadFailure, pointType));

    /// <summary>A pointwise objective of a private nested point type, nested privately itself.</summary>
    private readonly struct PrivatePointwise : IGpuPointwiseFitnessFunction<PrivateResidual>
    {
        /// <inheritdoc />
        public PrivateResidual EvaluatePoint(GeneView genes, int point) => new(genes[0]);

        /// <inheritdoc />
        public double Combine(GeneView genes, PointView<PrivateResidual> points) => points[0].Value;
    }

    /// <summary>A private nested point type.</summary>
    /// <param name="Value">The value.</param>
    private readonly record struct PrivateResidual(double Value);

    /// <summary>A private nested objective: ILGPU's runtime assembly cannot name it.</summary>
    private readonly struct PrivateSphere : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes) => genes[0] * genes[0];
    }

    /// <summary>A release that fails when disposed.</summary>
    /// <param name="failure">What it throws.</param>
    private sealed class FailingRelease(Exception failure) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => throw failure;
    }
}

/// <summary>An internal objective over any type: invisible when the type is.</summary>
/// <typeparam name="T">The type it wraps.</typeparam>
internal readonly struct Wrapping<T> : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes) => genes[0];
}
