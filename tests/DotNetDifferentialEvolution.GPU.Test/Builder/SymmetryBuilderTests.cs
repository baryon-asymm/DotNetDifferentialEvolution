using System.Globalization;
using System.Reflection;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Checks S1 and B2 of the GPU package's ACCEPTANCE.md. S1: the scheme stage has the CPU builder's methods, by name,
/// parameter names, types, order and defaults, except the two that take the CPU package's open interfaces. B2: every row
/// API.md's error table gained with the symmetry has a case that triggers it, with a boundary case beside each guard.
/// </summary>
public class SymmetryBuilderTests
{
    private static readonly double[] Lower = [-1.0, -1.0];
    private static readonly double[] Upper = [1.0, 1.0];

    /// <summary>The CPU methods that take an open interface of the CPU package, out of the GPU's scope (API.md).</summary>
    private static readonly string[] CpuOnly = ["WithMutationStrategy", "WithVariant"];

    /// <summary>Each scheme stage method with its smallest population.</summary>
    /// <returns>The method's name and N.</returns>
    public static TheoryData<string, int> Minimums() => new()
    {
        { nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy), 4 },
        { nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy), 3 },
        { nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy), 3 },
        { nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy), 6 },
        { nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy), 5 },
        { nameof(IGpuMutationStrategyRequired<>.WithJde), 4 },
        { nameof(IGpuMutationStrategyRequired<>.WithJade), 4 },
        { nameof(IGpuMutationStrategyRequired<>.WithShade), 4 },
        { nameof(IGpuMutationStrategyRequired<>.WithLShade), 4 },
    };

    /// <summary>S1: every CPU scheme method but the two open-interface ones has a GPU twin of the same signature and defaults, and no GPU method lacks a CPU twin.</summary>
    [Fact]
    public void TheSchemeStageHasTheCpuBuildersMethodsAndDefaults()
    {
        var cpu = typeof(IMutationStrategyRequired).GetMethods().Where(method => !CpuOnly.Contains(method.Name)).ToList();
        var gpu = typeof(IGpuMutationStrategyRequired<Sphere>).GetMethods().ToList();
        Assert.Equal(9, cpu.Count);

        Assert.Equal(cpu.Select(method => method.Name).Order(StringComparer.Ordinal), gpu.Select(method => method.Name).Order(StringComparer.Ordinal));
        foreach (var cpuMethod in cpu)
        {
            var gpuMethod = gpu.Single(method => method.Name == cpuMethod.Name);
            Assert.Equal(Signature(cpuMethod), Signature(gpuMethod));
        }
    }

    /// <summary>B2: a population below the scheme's minimum is an <see cref="InvalidOperationException"/> from <c>Build</c>, naming the method and the minimum; the minimum itself builds.</summary>
    /// <param name="method">The scheme stage method.</param>
    /// <param name="minimum">Its smallest population.</param>
    [Theory]
    [MemberData(nameof(Minimums))]
    public void APopulationBelowTheSchemesMinimumIsRefusedByBuild(string method, int minimum)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => Configure(method, minimum - 1).OnDevice(GpuDevice.Cpu).Build());
        Assert.Contains(method, failure.Message, StringComparison.Ordinal);
        Assert.Contains($"at least {minimum} ", failure.Message, StringComparison.Ordinal);

        using var optimizer = Configure(method, minimum).OnDevice(GpuDevice.Cpu).Build();
        Assert.Equal(GpuDevice.Cpu, optimizer.Device.Kind);
    }

    /// <summary>B2: a p-best rate outside (0, 1] is an <see cref="ArgumentOutOfRangeException"/> for JADE, SHADE and L-SHADE; 1 is accepted.</summary>
    /// <param name="pBestRate">p.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.0000001)]
    [InlineData(double.NaN)]
    public void APBestRateOutsideZeroToOneIsRejected(double pBestRate)
    {
        var stage = Stage(10);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithJade(pBestRate: pBestRate));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithShade(pBestRate: pBestRate));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithLShade(1000, pBestRate: pBestRate));
        _ = stage.WithJade(pBestRate: 1.0);
    }

    /// <summary>B2: an archive size rate that is negative or not finite is an <see cref="ArgumentOutOfRangeException"/>; 0 is accepted.</summary>
    /// <param name="archiveSizeRate">The rate.</param>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AnArchiveSizeRateThatIsNegativeOrNotFiniteIsRejected(double archiveSizeRate)
    {
        var stage = Stage(10);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithJade(archiveSizeRate: archiveSizeRate));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithShade(archiveSizeRate: archiveSizeRate));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithLShade(1000, archiveSizeRate: archiveSizeRate));
        _ = stage.WithShade(archiveSizeRate: 0.0);
    }

    /// <summary>B2: JADE's adaptation rate outside [0, 1] is an <see cref="ArgumentOutOfRangeException"/>; 0 and 1 are accepted.</summary>
    /// <param name="adaptationRate">c.</param>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void AnAdaptationRateOutsideZeroToOneIsRejected(double adaptationRate)
    {
        var stage = Stage(10);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithJade(adaptationRate: adaptationRate));
        _ = stage.WithJade(adaptationRate: 0.0);
        _ = stage.WithJade(adaptationRate: 1.0);
    }

    /// <summary>B2: a memory size below 1 is an <see cref="ArgumentOutOfRangeException"/> for SHADE and L-SHADE; 1 is accepted.</summary>
    [Fact]
    public void AMemorySizeBelowOneIsRejected()
    {
        var stage = Stage(10);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithShade(memorySize: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithLShade(1000, memorySize: 0));
        _ = stage.WithShade(memorySize: 1);
    }

    /// <summary>B2: an L-SHADE budget below 1 is an <see cref="ArgumentOutOfRangeException"/>; 1 is accepted.</summary>
    [Fact]
    public void AnLShadeBudgetBelowOneIsRejected()
    {
        var stage = Stage(10);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithLShade(0));
        _ = stage.WithLShade(1);
    }

    /// <summary>B2: jDE's initial F not finite or not positive, or its initial CR outside [0, 1], is an <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="initialMutationForce">F.</param>
    /// <param name="initialCrossoverProbability">CR.</param>
    [Theory]
    [InlineData(0.0, 0.9)]
    [InlineData(-0.5, 0.9)]
    [InlineData(double.NaN, 0.9)]
    [InlineData(double.PositiveInfinity, 0.9)]
    [InlineData(0.5, -0.1)]
    [InlineData(0.5, 1.1)]
    [InlineData(0.5, double.NaN)]
    public void JdesInitialParametersOutsideTheirRangesAreRejected(double initialMutationForce, double initialCrossoverProbability)
    {
        var stage = Stage(10);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithJde(initialMutationForce, initialCrossoverProbability));
        _ = stage.WithJde(double.Epsilon, 0.0);
        _ = stage.WithJde(0.5, 1.0);
    }

    /// <summary>B2: a stagnation streak below 1, or a threshold negative or not finite, is an <see cref="ArgumentOutOfRangeException"/>; 1 and 0 are accepted.</summary>
    /// <param name="maxStagnationStreak">The streak.</param>
    /// <param name="stagnationThreshold">The threshold.</param>
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(-1, 0.0)]
    [InlineData(10, -1e-12)]
    [InlineData(10, double.NaN)]
    [InlineData(10, double.PositiveInfinity)]
    public void AStagnationLimitOutsideItsRangesIsRejected(int maxStagnationStreak, double stagnationThreshold)
    {
        var stage = Stage(10).WithDefaultMutationStrategy(0.5, 0.9);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithStagnationLimit(maxStagnationStreak, stagnationThreshold));
        _ = stage.WithStagnationLimit(1, 0.0);
    }

    /// <summary>B2: L-SHADE with an evaluation limit other than its budget is an <see cref="InvalidOperationException"/> from <c>Build</c>, as the CPU package's <c>LShadeVariant.Validate</c>; the same budget builds.</summary>
    [Fact]
    public void LShadeWithAnotherEvaluationLimitIsRefusedByBuild()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => Stage(10).WithLShade(1000).WithEvaluationLimit(999).OnDevice(GpuDevice.Cpu).Build());
        Assert.Contains("1000", failure.Message, StringComparison.Ordinal);
        Assert.Contains("999", failure.Message, StringComparison.Ordinal);

        using var optimizer = Stage(10).WithLShade(1000).WithEvaluationLimit(1000).OnDevice(GpuDevice.Cpu).Build();
        Assert.Equal(GpuDevice.Cpu, optimizer.Device.Kind);
    }

    /// <summary>
    /// B2: an archive of more than <see cref="int.MaxValue"/> genes is an <see cref="InvalidOperationException"/> from
    /// <c>Build</c>. At the edge: N = 16 and a rate of 2²⁶ give a capacity of 2³⁰ and, with D = 2, 2³¹ genes, one more than
    /// <see cref="int.MaxValue"/> (and a product that overflows an <see langword="int"/>). The largest archive that passes
    /// would allocate 16 GiB, so the passing side is not built.
    /// </summary>
    [Fact]
    public void AnArchiveBeyondTheIndexRangeIsRefusedByBuild()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => Stage(16).WithJade(archiveSizeRate: 1 << 26).WithGenerationLimit(1).OnDevice(GpuDevice.Cpu).Build());
        Assert.Contains("archive", failure.Message, StringComparison.Ordinal);
        Assert.Contains("2147483648 genes", failure.Message, StringComparison.Ordinal);
    }

    private static IGpuMutationStrategyRequired<Sphere> Stage(int populationSize) =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(Lower, Upper).WithPopulationSize(populationSize);

    private static IGpuDeviceRequired<Sphere> Configure(string method, int populationSize)
    {
        var stage = Stage(populationSize);
        var limited = method switch
        {
            nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy) => stage.WithDefaultMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy) => stage.WithBestMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy) => stage.WithCurrentToBestMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy) => stage.WithRandTwoMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy) => stage.WithBestTwoMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithJde) => stage.WithJde(),
            nameof(IGpuMutationStrategyRequired<>.WithJade) => stage.WithJade(),
            nameof(IGpuMutationStrategyRequired<>.WithShade) => stage.WithShade(),
            nameof(IGpuMutationStrategyRequired<>.WithLShade) => stage.WithLShade(1000),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Not a scheme stage method."),
        };
        return limited.WithGenerationLimit(1);
    }

    /// <summary>A method's parameters as text: name, type and default, in order.</summary>
    private static string Signature(MethodInfo method) =>
        string.Join(", ", method.GetParameters().Select(parameter =>
            string.Create(CultureInfo.InvariantCulture, $"{parameter.ParameterType.Name} {parameter.Name}")
            + (parameter.HasDefaultValue ? string.Create(CultureInfo.InvariantCulture, $" = {parameter.DefaultValue}") : string.Empty)));
}
