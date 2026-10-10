using DotNetDifferentialEvolution.GPU.Devices;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// The collection of the tests that count <see cref="KernelLoader.LoadCount"/>, a number of the whole process: it runs
/// alone, after the tests that run in parallel, so that no other load is counted. The one test of its own: every load
/// counts once.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
[Collection(Name)]
public sealed class KernelLoadCounting
{
    /// <summary>The name of the collection.</summary>
    public const string Name = "KernelLoadCount";

    /// <summary>Every <see cref="KernelLoader.Load"/> adds one to <see cref="KernelLoader.LoadCount"/>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void EveryLoadCountsOnce()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        var method = typeof(OwnershipTests).GetMethod(nameof(OwnershipTests.FillWithIndex), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var before = KernelLoader.LoadCount;

        using var first = KernelLoader.Load(accelerator, method, 1);
        using var second = KernelLoader.Load(accelerator, method, 1);

        Assert.Equal(before + 2, KernelLoader.LoadCount);
    }
}

/// <summary>
/// Check A10 of the Kernels node's ACCEPTANCE.md, kernels compiled in <c>Build</c>, once. For JADE, SHADE and L-SHADE with a
/// stagnation limit, and a pointwise SHADE run, on the CPU accelerator: <c>KernelLoader.LoadCount</c> after <c>Build</c> has
/// grown by the number of distinct kernels of the configuration, and the run adds none. The kernels of a configuration are
/// listed here by name, from what the launcher and the bookkeeping do: the launcher's two (initialise, generation) or five
/// (sample, evaluate points, combine, build trials, select); the two fills; ranking by counting (current-to-pbest, N below
/// the counting limit); the best index (two) beside a stagnation rule; the archive's four; the adaptation's two and, for SHADE
/// and L-SHADE, the largest weights; L-SHADE's reduction; the stagnation pass.
/// </summary>
/// <param name="output">Receives the counts.</param>
[Collection(KernelLoadCounting.Name)]
public class KernelLoadCountTests(ITestOutputHelper output)
{
    private const int GenomeSize = PairArithmetic.GenomeSize;
    private const int PopulationSize = 32;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, GenomeSize)];

    private static readonly string[] Fills = ["FillInts", "FillDoubles"];
    private static readonly string[] Ranking = ["RankByCounting"];
    private static readonly string[] BestIndex = ["BestOfChunks", "BestOfPartials"];
    private static readonly string[] Archive = ["CountImproved", "ScanImproved", "PlaceImproved", "CopyToArchive"];
    private static readonly string[] Adaptation = ["SumSuccesses", "Adapt"];
    private static readonly string[] MonolithicLauncher = ["Initialize", "Generation"];
    private static readonly string[] PointwiseLauncher = ["Sample", "EvaluatePoints", "CombineInitial", "BuildTrials", "Select"];

    /// <summary>A10 for JADE with a stagnation limit.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task JadeWithAStagnationLimitLoadsItsKernelsInBuild() =>
        AssertLoadedInBuild(
            "JADE",
            [.. MonolithicLauncher, .. Fills, .. Ranking, .. BestIndex, .. Archive, .. Adaptation, "Stagnate"],
            accelerator => Stepped().WithJade().WithStagnationLimit(40, 0.0).OnAccelerator(accelerator).WithSeed(7).Build());

    /// <summary>A10 for SHADE with a stagnation limit.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task ShadeWithAStagnationLimitLoadsItsKernelsInBuild() =>
        AssertLoadedInBuild(
            "SHADE",
            [.. MonolithicLauncher, .. Fills, .. Ranking, .. BestIndex, .. Archive, .. Adaptation, "LargestWeights", "Stagnate"],
            accelerator => Stepped().WithShade().WithStagnationLimit(40, 0.0).OnAccelerator(accelerator).WithSeed(7).Build());

    /// <summary>A10 for L-SHADE with a stagnation limit.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task LShadeWithAStagnationLimitLoadsItsKernelsInBuild() =>
        AssertLoadedInBuild(
            "L-SHADE",
            [.. MonolithicLauncher, .. Fills, .. Ranking, .. BestIndex, .. Archive, .. Adaptation, "LargestWeights", "Compact", "Stagnate"],
            accelerator => Stepped().WithLShade(20_000).WithStagnationLimit(40, 0.0).OnAccelerator(accelerator).WithSeed(7).Build());

    /// <summary>A10 for a pointwise SHADE run.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task APointwiseShadeRunLoadsItsKernelsInBuild() =>
        AssertLoadedInBuild(
            "pointwise SHADE",
            [.. PointwiseLauncher, .. Fills, .. Ranking, .. Archive, .. Adaptation, "LargestWeights"],
            accelerator => GpuDifferentialEvolutionBuilder
                .ForPointwiseFunction<PairPointwise, PairPoint>(default, PairArithmetic.PointCount)
                .WithBounds(Lower, Upper)
                .WithPopulationSize(PopulationSize)
                .WithShade()
                .WithGenerationLimit(10)
                .OnAccelerator(accelerator)
                .WithSeed(1)
                .Build());

    private static IGpuMutationStrategyRequired<SymmetryRunTests.SteppedSphere> Stepped() =>
        GpuDifferentialEvolutionBuilder
            .ForFunction(default(SymmetryRunTests.SteppedSphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(PopulationSize);

    private async Task AssertLoadedInBuild(string name, string[] kernels, Func<Accelerator, GpuDifferentialEvolution> build)
    {
        Assert.Equal(kernels.Length, kernels.Distinct(StringComparer.Ordinal).Count());
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);

        var before = KernelLoader.LoadCount;
        using var optimizer = build(accelerator);
        var built = KernelLoader.LoadCount - before;
        output.WriteLine($"{name}: {built} kernels loaded by Build, {kernels.Length} expected ({string.Join(", ", kernels)})");
        Assert.Equal(kernels.Length, built);

        _ = await optimizer.RunAsync().ConfigureAwait(true);

        Assert.Equal(before + built, KernelLoader.LoadCount);
    }
}
