using ILGPU;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Check B1 of the GPU package's ACCEPTANCE.md for the builder: every row of API.md's "Errors of
/// v1" table that a builder stage or <c>Build</c> raises has a case that triggers it, each in its
/// own test, plus the guards the implementation adds (non-finite bounds, N·D beyond
/// <see cref="int.MaxValue"/>, an undefined <see cref="GpuDevice"/>, an accelerator of another
/// type). A boundary case next to each guard shows the guard is not wider than the contract.
/// The device row lives in <c>Devices</c>; the run rows in <c>EndToEnd</c>.
/// </summary>
public class BuilderErrorTests
{
    private static readonly double[] Lower = [-1.0, -1.0];
    private static readonly double[] Upper = [1.0, 1.0];
    private static readonly double[] OneGene = [1.0];
    private static readonly double[] CrossedLower = [0.0, 2.0];
    private static readonly double[] CrossedUpper = [1.0, 1.5];
    private static readonly double[] FlatLower = [0.5, -1.0];
    private static readonly double[] FlatUpper = [0.5, 1.0];

    /// <summary>Lower and upper bounds of different lengths are an <see cref="ArgumentException"/> from <c>WithBounds</c>.</summary>
    [Fact]
    public void BoundsOfDifferentLengthsAreRejected()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));

        _ = Assert.Throws<ArgumentException>(() => stage.WithBounds(Upper, OneGene));
    }

    /// <summary>Empty bounds are an <see cref="ArgumentException"/> from <c>WithBounds</c>.</summary>
    [Fact]
    public void EmptyBoundsAreRejected()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));

        _ = Assert.Throws<ArgumentException>(() => stage.WithBounds(ReadOnlyMemory<double>.Empty, ReadOnlyMemory<double>.Empty));
    }

    /// <summary>A lower bound above its upper bound, in any gene, is an <see cref="ArgumentException"/> from <c>WithBounds</c>.</summary>
    [Fact]
    public void ALowerBoundAboveItsUpperBoundIsRejected()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));

        _ = Assert.Throws<ArgumentException>(() => stage.WithBounds(CrossedLower, CrossedUpper));
    }

    /// <summary>Equal bounds, a box of zero width in a gene, are accepted: only lower &gt; upper is an error.</summary>
    [Fact]
    public void EqualBoundsAreAccepted()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));

        Assert.NotNull(stage.WithBounds(FlatLower, FlatUpper));
    }

    /// <summary>A non-finite bound, lower or upper, is an <see cref="ArgumentException"/> from <c>WithBounds</c>.</summary>
    /// <param name="lower">The lower bound of gene 1.</param>
    /// <param name="upper">The upper bound of gene 1.</param>
    [Theory]
    [InlineData(double.NaN, 1.0)]
    [InlineData(0.0, double.NaN)]
    [InlineData(double.NegativeInfinity, 1.0)]
    [InlineData(0.0, double.PositiveInfinity)]
    public void NonFiniteBoundsAreRejected(double lower, double upper)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));

        _ = Assert.Throws<ArgumentException>(() => stage.WithBounds(new double[] { 0.0, lower }, new double[] { 1.0, upper }));
    }

    /// <summary>
    /// A population of fewer than one is an <see cref="ArgumentOutOfRangeException"/>. The scheme's own minimum is refused
    /// by <c>Build</c> (<see cref="SymmetryBuilderTests"/>). ⚠ 2026-10-05: was "fewer than four", rand/1's minimum, here.
    /// </summary>
    /// <param name="populationSize">N.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APopulationOfFewerThanOneIsRejected(int populationSize)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationSize(populationSize));
    }

    /// <summary>A population of exactly four is accepted.</summary>
    [Fact]
    public void APopulationOfFourIsAccepted()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper);

        Assert.NotNull(stage.WithPopulationSize(4));
    }

    /// <summary>
    /// A population whose N·D exceeds <see cref="int.MaxValue"/> is an
    /// <see cref="ArgumentOutOfRangeException"/>: a kernel indexes genes with an <see cref="int"/>.
    /// With D = 2, N = 2³⁰ gives exactly 2³¹ genes, one more than the limit; 2³⁰ − 1 fits.
    /// </summary>
    [Fact]
    public void APopulationBeyondTheKernelsIndexRangeIsRejected()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationSize(1 << 30));
        Assert.NotNull(stage.WithPopulationSize((1 << 30) - 1));
    }

    /// <summary>F that is not finite, or ≤ 0, is an <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="mutationForce">F.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    public void AMutationForceNotFiniteOrNotPositiveIsRejected(double mutationForce)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper).WithPopulationSize(8);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithDefaultMutationStrategy(mutationForce, 0.5));
    }

    /// <summary>CR outside [0, 1], or <see cref="double.NaN"/>, is an <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="crossoverProbability">CR.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void ACrossoverProbabilityOutsideTheUnitIntervalIsRejected(double crossoverProbability)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper).WithPopulationSize(8);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithDefaultMutationStrategy(0.5, crossoverProbability));
    }

    /// <summary>The edges of the valid ranges are accepted: the smallest positive F, CR = 0 and CR = 1.</summary>
    /// <param name="mutationForce">F.</param>
    /// <param name="crossoverProbability">CR.</param>
    [Theory]
    [InlineData(double.Epsilon, 0.0)]
    [InlineData(2.0, 1.0)]
    public void TheEdgesOfFAndCrAreAccepted(double mutationForce, double crossoverProbability)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper).WithPopulationSize(8);

        Assert.NotNull(stage.WithDefaultMutationStrategy(mutationForce, crossoverProbability));
    }

    /// <summary>A generation limit below 1 is an <see cref="ArgumentOutOfRangeException"/>; 1 is accepted.</summary>
    /// <param name="maxGenerations">The limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AGenerationLimitBelowOneIsRejected(int maxGenerations)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper).WithPopulationSize(8).WithDefaultMutationStrategy(0.5, 0.5);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithGenerationLimit(maxGenerations));
        Assert.NotNull(stage.WithGenerationLimit(1));
    }

    /// <summary>An evaluation limit below 1 is an <see cref="ArgumentOutOfRangeException"/>; 1 is accepted.</summary>
    /// <param name="maxEvaluations">The limit.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void AnEvaluationLimitBelowOneIsRejected(long maxEvaluations)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper).WithPopulationSize(8).WithDefaultMutationStrategy(0.5, 0.5);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithEvaluationLimit(maxEvaluations));
        Assert.NotNull(stage.WithEvaluationLimit(1L));
    }

    /// <summary>An observer period below 1 is an <see cref="ArgumentOutOfRangeException"/>; 1 is accepted.</summary>
    /// <param name="everyNGenerations">The period.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnObserverPeriodBelowOneIsRejected(int everyNGenerations)
    {
        var stage = CpuStage();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationUpdateHandler(new IgnoringHandler(), everyNGenerations));
        Assert.NotNull(stage.WithPopulationUpdateHandler(new IgnoringHandler(), 1));
    }

    /// <summary>A <see langword="null"/> observer is an <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void ANullObserverIsRejected()
    {
        var stage = CpuStage();

        _ = Assert.Throws<ArgumentNullException>(() => stage.WithPopulationUpdateHandler(null!));
    }

    /// <summary>A <see langword="null"/> accelerator is an <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void ANullAcceleratorIsRejected()
    {
        var stage = DeviceStage();

        _ = Assert.Throws<ArgumentNullException>(() => stage.OnAccelerator(null!));
    }

    /// <summary>A <see cref="GpuDevice"/> value outside the enum is an <see cref="ArgumentOutOfRangeException"/> from <c>OnDevice</c>.</summary>
    /// <param name="device">The undefined value.</param>
    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    public void AnUndefinedDeviceIsRejected(int device)
    {
        var stage = DeviceStage();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.OnDevice((GpuDevice)device));
    }

    /// <summary>
    /// An accelerator that is not CUDA, OpenCL or CPU is an <see cref="ArgumentException"/> from
    /// <c>OnAccelerator</c>. ILGPU 1.5.3 defines no other type, so the case uses a hand-made one
    /// (<see cref="ForeignAccelerator"/>).
    /// </summary>
    [Fact]
    public void AnAcceleratorOfAnotherTypeIsRejected()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = new ForeignDevice().CreateAccelerator(context);
        var stage = DeviceStage();

        Assert.Equal(ForeignAccelerator.ForeignType, accelerator.AcceleratorType);
        _ = Assert.Throws<ArgumentException>(() => stage.OnAccelerator(accelerator));
    }

    /// <summary>
    /// An objective ILGPU cannot compile (a <c>throw</c> in its body) makes <c>Build</c> throw
    /// ILGPU's own exception, not a wrapper of the package's. Runs on the CPU accelerator, which
    /// compiles kernels through the same frontend as CUDA and OpenCL.
    /// </summary>
    [Fact]
    public void AnObjectiveIlgpuCannotCompileFailsBuildWithIlgpusException()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Throwing))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(8)
            .WithDefaultMutationStrategy(0.5, 0.5)
            .WithGenerationLimit(1)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1);

        _ = Assert.Throws<InternalCompilerException>(stage.Build);
    }

    private static IGpuDeviceRequired<Sphere> DeviceStage() =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(8)
            .WithDefaultMutationStrategy(0.5, 0.5)
            .WithGenerationLimit(1);

    private static IGpuDifferentialEvolutionBuilder<Sphere> CpuStage() => DeviceStage().OnDevice(GpuDevice.Cpu);
}
