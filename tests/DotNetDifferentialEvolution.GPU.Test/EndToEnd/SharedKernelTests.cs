using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A1 of the Devices node's ACCEPTANCE.md, no kernel shared between loads, over whole runs. Optimizers built with
/// <c>OnAccelerator</c> on one caller's accelerator load their kernels through <c>KernelLoader</c>, which compiles each load
/// explicitly: disposing one optimizer leaves the others' kernels alone. On the CPU accelerator (CI): two optimizers (JADE,
/// Sphere D = 3 in [−5, 5], N = 32, 20 generations, seeds 1 and 2), the second run after the first's <c>Dispose</c> equals
/// its run alone bit for bit, and a third built afterwards (seed 3) equals its run alone. On OpenCL, under <c>Gpu</c>: the
/// same, and four optimizers running at once on one accelerator (JADE, Sphere D = 10, N = 64, 200 generations, an observer
/// every 10, seeds 1 to 4), ten repeats, each equal to its run alone, result and every snapshot.
/// </summary>
public class SharedKernelTests
{
    private const int ConcurrentOptimizers = 4;
    private const int Repeats = 10;

    /// <summary>A1 on the CPU accelerator: the second optimizer survives the first's disposal and a third is built after it.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ASecondOptimizerSurvivesTheFirstsDisposalOnTheCpuAccelerator()
    {
        using var referenceContext = Context.Create(builder => builder.CPU());
        using var reference = referenceContext.CreateCPUAccelerator(0);
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);

        await AssertASecondOptimizerSurvives(accelerator, reference).ConfigureAwait(true);
    }

    /// <summary>A1 on OpenCL: the same as on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public async Task ASecondOptimizerSurvivesTheFirstsDisposalOnOpenCl()
    {
        using var context = Context.Create(builder => builder.OpenCL());
        using var accelerator = context.CreateCLAccelerator(0);

        await AssertASecondOptimizerSurvives(accelerator, accelerator).ConfigureAwait(true);
    }

    /// <summary>A1 on OpenCL: four optimizers running at once on one accelerator, ten times, each equal to its run alone.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public async Task FourOptimizersRunningAtOnceOnOpenClEachEqualItsRunAlone()
    {
        using var context = Context.Create(builder => builder.OpenCL());
        using var accelerator = context.CreateCLAccelerator(0);
        var alone = new RunRecord[ConcurrentOptimizers];
        for (var k = 0; k < ConcurrentOptimizers; k++)
        {
            alone[k] = await RunAlone(accelerator, k + 1, genomeSize: 10, populationSize: 64, generations: 200).ConfigureAwait(true);
        }

        for (var repeat = 0; repeat < Repeats; repeat++)
        {
            var hashers = Enumerable.Range(0, ConcurrentOptimizers).Select(_ => new SnapshotHasher()).ToArray();
            var optimizers = new GpuDifferentialEvolution[ConcurrentOptimizers];
            try
            {
                for (var k = 0; k < ConcurrentOptimizers; k++)
                {
                    optimizers[k] = Build(accelerator, k + 1, 10, 64, 200, hashers[k]);
                }

                var results = await Task.WhenAll(optimizers.Select(optimizer => Task.Run(() => optimizer.RunAsync()))).ConfigureAwait(true);

                for (var k = 0; k < ConcurrentOptimizers; k++)
                {
                    RunRecord.AssertSame(alone[k], Record(results[k], hashers[k]), $"repeat {repeat}, seed {k + 1}");
                }
            }
            finally
            {
                foreach (var optimizer in optimizers)
                {
                    optimizer?.Dispose();
                }
            }
        }
    }

    private static async Task AssertASecondOptimizerSurvives(Accelerator accelerator, Accelerator reference)
    {
        var firstAlone = await RunAlone(reference, 1, genomeSize: 3, populationSize: 32, generations: 20).ConfigureAwait(true);
        var secondAlone = await RunAlone(reference, 2, genomeSize: 3, populationSize: 32, generations: 20).ConfigureAwait(true);
        var thirdAlone = await RunAlone(reference, 3, genomeSize: 3, populationSize: 32, generations: 20).ConfigureAwait(true);

        var hasherOfFirst = new SnapshotHasher();
        var hasherOfSecond = new SnapshotHasher();
        var hasherOfThird = new SnapshotHasher();
        using var second = Build(accelerator, 2, 3, 32, 20, hasherOfSecond);
        RunRecord firstRun;
        using (var first = Build(accelerator, 1, 3, 32, 20, hasherOfFirst))
        {
            firstRun = Record(await first.RunAsync().ConfigureAwait(true), hasherOfFirst);
        }

        var secondRun = Record(await second.RunAsync().ConfigureAwait(true), hasherOfSecond);
        RunRecord thirdRun;
        using (var third = Build(accelerator, 3, 3, 32, 20, hasherOfThird))
        {
            thirdRun = Record(await third.RunAsync().ConfigureAwait(true), hasherOfThird);
        }

        RunRecord.AssertSame(firstAlone, firstRun, "the first optimizer");
        RunRecord.AssertSame(secondAlone, secondRun, "the second optimizer, run after the first's disposal");
        RunRecord.AssertSame(thirdAlone, thirdRun, "the third optimizer, built after the first's disposal");
    }

    private static async Task<RunRecord> RunAlone(Accelerator accelerator, int seed, int genomeSize, int populationSize, int generations)
    {
        var hasher = new SnapshotHasher();
        using var optimizer = Build(accelerator, seed, genomeSize, populationSize, generations, hasher);
        return Record(await optimizer.RunAsync().ConfigureAwait(true), hasher);
    }

    private static GpuDifferentialEvolution Build(
        Accelerator accelerator,
        int seed,
        int genomeSize,
        int populationSize,
        int generations,
        SnapshotHasher hasher) =>
        GpuDifferentialEvolutionBuilder
            .ForFunction(default(Sphere))
            .WithBounds(Enumerable.Repeat(-5.0, genomeSize).ToArray(), Enumerable.Repeat(5.0, genomeSize).ToArray())
            .WithPopulationSize(populationSize)
            .WithJade()
            .WithGenerationLimit(generations)
            .OnAccelerator(accelerator)
            .WithSeed(seed)
            .WithPopulationUpdateHandler(hasher, 10)
            .Build();

    private static RunRecord Record(GpuOptimizationResult result, SnapshotHasher hasher) =>
        new(
            result.Generations,
            result.EvaluationCount,
            BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue),
            [.. result.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits)],
            hasher.Hashes);
}
