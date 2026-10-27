using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// The kernels <c>Build</c> compiles for an initial population (checks A2 and A10 with I5): a sampled run, in the search box
/// or in an initial box, compiles the sampling kernel and not the evaluation-only one; a run from given genes compiles the
/// evaluation-only one and not the sampling kernel. Read from the source methods of the kernels the optimizer holds, on the CPU
/// accelerator, for the monolithic and the pointwise objective.
/// </summary>
public class InitialKernelSelectionTests
{
    private static readonly double[] Lower = [-10.0, -10.0, -10.0];
    private static readonly double[] Upper = [10.0, 10.0, 10.0];
    private static readonly double[] Genes = [.. Enumerable.Repeat(1.0, 8 * 3)];

    /// <summary>A sampled monolithic run compiles <c>Initialize</c> and <c>Generation</c>, and not <c>EvaluateInitial</c>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void ASampledMonolithicRunCompilesTheSamplingKernelOnly()
    {
        using var optimizer = Stage(GpuDifferentialEvolutionBuilder.ForFunction(default(FiveSphere))).Build();

        var names = KernelNames(optimizer);

        Assert.Contains("Initialize", names);
        Assert.Contains("Generation", names);
        Assert.DoesNotContain("EvaluateInitial", names);
    }

    /// <summary>A monolithic run in an initial box compiles the same kernels as a sampled one.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void AMonolithicRunInAnInitialBoxCompilesTheSamplingKernelOnly()
    {
        using var optimizer = Stage(GpuDifferentialEvolutionBuilder.ForFunction(default(FiveSphere))).WithInitialBounds(Lower, Lower).Build();

        var names = KernelNames(optimizer);

        Assert.Contains("Initialize", names);
        Assert.DoesNotContain("EvaluateInitial", names);
    }

    /// <summary>A monolithic run from given genes compiles <c>EvaluateInitial</c> and <c>Generation</c>, and not <c>Initialize</c>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void AMonolithicWarmStartCompilesTheEvaluatingKernelOnly()
    {
        using var optimizer = Stage(GpuDifferentialEvolutionBuilder.ForFunction(default(FiveSphere))).WithInitialPopulation(Genes).Build();

        var names = KernelNames(optimizer);

        Assert.Contains("EvaluateInitial", names);
        Assert.Contains("Generation", names);
        Assert.DoesNotContain("Initialize", names);
    }

    /// <summary>A sampled pointwise run compiles <c>Sample</c> with the other four kernels.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void ASampledPointwiseRunCompilesTheSamplingKernel()
    {
        using var optimizer = Stage(PointwiseStart()).Build();

        var names = KernelNames(optimizer);

        Assert.Equal(["BuildTrials", "CombineInitial", "EvaluatePoints", "Sample", "Select"], names);
    }

    /// <summary>A pointwise warm start compiles the four kernels that are not <c>Sample</c>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void APointwiseWarmStartDoesNotCompileTheSamplingKernel()
    {
        using var optimizer = Stage(PointwiseStart()).WithInitialPopulation(Genes).Build();

        var names = KernelNames(optimizer);

        Assert.Equal(["BuildTrials", "CombineInitial", "EvaluatePoints", "Select"], names);
    }

    private static IGpuBoundsRequired<FiveSpherePointwise> PointwiseStart() =>
        GpuDifferentialEvolutionBuilder.ForPointwiseFunction<FiveSpherePointwise, SquaredDeviation>(default, 3);

    private static IGpuDifferentialEvolutionBuilder<TFunction> Stage<TFunction>(IGpuBoundsRequired<TFunction> start)
        where TFunction : struct =>
        start
            .WithBounds(Lower, Upper)
            .WithPopulationSize(8)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(2)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1);

    private static string[] KernelNames(GpuDifferentialEvolution optimizer) =>
        [.. optimizer.Allocated
            .OfType<Kernel>()
            .Select(kernel => kernel.CompiledKernel.SourceMethod.Name)
            .Where(name => name is "Initialize" or "EvaluateInitial" or "Sample" or "EvaluatePoints" or "CombineInitial" or "BuildTrials" or "Select" or "Generation")
            .Order(StringComparer.Ordinal)];
}
