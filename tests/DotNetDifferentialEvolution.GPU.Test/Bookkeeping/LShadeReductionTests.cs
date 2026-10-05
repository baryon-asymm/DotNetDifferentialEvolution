using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.GPU.Test.Kernels;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.TerminationStrategies;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// ACCEPTANCE.md, check S11: L-SHADE's linear population size reduction.
/// <list type="bullet">
/// <item>The schedule's known answers, computed outside the package (2026-10-05, exact decimal rounding): for N_init 100
/// and a budget of 10 000, 98, 97, 96, 95, 94, 93, 93, 92, 91, 90, 89, 88 first, 333 generations, N = 4 and 10 000
/// evaluations at the end; for N_init 5 and a budget of 20, 5, 4, 4, 4, whose first value is a midpoint (4.5).</item>
/// <item>A run of the CPU package and a run on the CPU accelerator, both L-SHADE 100 / 10 000, give that N each
/// generation; the GPU run's population is sorted after each reduction, as the survivors are stored in ranking order.</item>
/// <item>The reduction pass keeps the first N of the ranking in order, makes the ranking the identity, and cuts the archive
/// to its new capacity.</item>
/// </list>
/// Building with an evaluation limit other than the budget is check B2's
/// (<c>SymmetryBuilderTests.LShadeWithAnotherEvaluationLimitIsRefusedByBuild</c>).
/// </summary>
[Trait("Category", "Integration")]
public class LShadeReductionTests
{
    private static readonly int[] FirstTwelve = [98, 97, 96, 95, 94, 93, 93, 92, 91, 90, 89, 88];

    /// <summary>The schedule gives the known answers.</summary>
    [Fact]
    public void TheScheduleGivesTheKnownAnswers()
    {
        var (sizes, evaluations) = Schedule(100, 10_000);
        Assert.Equal(FirstTwelve, sizes.Take(12));
        Assert.Equal(333, sizes.Count);
        Assert.Equal(4, sizes[^1]);
        Assert.Equal(10_000L, evaluations);

        var (midpoint, midpointEvaluations) = Schedule(5, 20);
        Assert.Equal([5, 4, 4, 4], midpoint);
        Assert.Equal(23L, midpointEvaluations);
    }

    /// <summary>The CPU package and the GPU package shrink their populations alike, and the GPU's is sorted after each reduction.</summary>
    [Fact]
    public async Task TheCpuAndTheGpuPackagesShrinkAlike()
    {
        var (expected, _) = Schedule(100, 10_000);
        double[] lower = [-5.0, -5.0];
        double[] upper = [5.0, 5.0];

        var cpuSizes = new CpuSizes();
        using (var cpu = DifferentialEvolutionBuilder
                   .ForFunction(new CpuSphere())
                   .WithBounds(lower, upper)
                   .WithPopulationSize(100)
                   .WithUniformPopulationSampling()
                   .WithLShade(10_000)
                   .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(10_000))
                   .UseProcessors(1)
                   .WithPopulationUpdateHandler(cpuSizes)
                   .WithSeed(1)
                   .Build())
        {
            _ = await cpu.RunAsync().ConfigureAwait(true);
        }

        var gpuSizes = new GpuSizes(100);
        using var gpu = GpuDifferentialEvolutionBuilder
            .ForFunction(default(GpuSphere))
            .WithBounds(lower, upper)
            .WithPopulationSize(100)
            .WithLShade(10_000)
            .WithEvaluationLimit(10_000)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1)
            .WithPopulationUpdateHandler(gpuSizes)
            .Build();
        var result = await gpu.RunAsync().ConfigureAwait(true);

        Assert.Equal(expected, cpuSizes.Sizes);
        Assert.Equal(expected, gpuSizes.Sizes);
        Assert.Equal(10_000L, result.EvaluationCount);
        Assert.True(gpuSizes.SortedAfterEachReduction, "a reduced population was not in ranking order");
    }

    /// <summary>The reduction pass keeps the ranking's first N in order, makes the ranking the identity, and cuts the archive.</summary>
    [Fact]
    public void TheReductionKeepsTheRankingsFirstInOrder()
    {
        const int populationSize = 50;
        const int genomeSize = 2;
        const int survivors = 30;
        var random = new SeededRandomProvider(20261011);
        var genes = new double[populationSize * genomeSize];
        var fitness = new double[populationSize];
        for (var k = 0; k < genes.Length; k++)
        {
            genes[k] = random.NextDouble();
        }

        for (var i = 0; i < populationSize; i++)
        {
            fitness[i] = random.Next(10) == 0 ? double.NaN : random.Next(20);
        }

        using var step = new HostStep();
        using var bookkeeping = new GenerationBookkeeping(
            step.Accelerator,
            new BookkeepingPlan(populationSize, genomeSize, SchemeKind.CurrentToPBest, ParameterRule.Shade, 40, 6, 0.0, true, double.NaN, double.NaN, null),
            seed: 1);
        using var current = step.Upload(genes);
        using var currentFitness = step.Upload(fitness);
        using var next = step.Upload(new double[genes.Length]);
        using var nextFitness = step.Upload(new double[populationSize]);
        using var unused = step.Upload([0.0]);
        bookkeeping.Views.ArchiveSize.CopyFromCPU([40, 0]);
        bookkeeping.Views.Outcomes.CopyFromCPU(new int[populationSize]);
        var views = new PopulationViews(current.View, currentFitness.View, next.View, nextFitness.View, unused.View, unused.View, unused.View);

        bookkeeping.AfterGeneration(ref views, 1, populationSize, 40, survivors, 10);
        step.Accelerator.Synchronize();

        var order = Enumerable.Range(0, populationSize).OrderBy(i => FitnessOrder.KeyOf(fitness[i])).ThenBy(i => i).Take(survivors).ToArray();
        var keptGenes = new double[survivors * genomeSize];
        var keptFitness = new double[survivors];
        var ranking = new int[survivors];
        var archiveSize = new int[2];
        views.Current.SubView(0, keptGenes.Length).CopyToCPU(keptGenes);
        views.CurrentFitness.SubView(0, survivors).CopyToCPU(keptFitness);
        bookkeeping.Views.Ranking.SubView(0, survivors).CopyToCPU(ranking);
        bookkeeping.Views.ArchiveSize.CopyToCPU(archiveSize);

        ParityCases.AssertSameBits([.. order.SelectMany(i => genes.Skip(i * genomeSize).Take(genomeSize))], keptGenes, "survivors' genes");
        ParityCases.AssertSameBits([.. order.Select(i => fitness[i])], keptFitness, "survivors' fitness");
        Assert.Equal(Enumerable.Range(0, survivors), ranking);
        Assert.Equal(10, archiveSize[0]);
    }

    /// <summary>N per generation and the evaluations at the end, by <see cref="LShadeSchedule.NextPopulationSize"/>.</summary>
    private static (List<int> Sizes, long Evaluations) Schedule(int initial, long budget)
    {
        var sizes = new List<int>();
        var size = initial;
        long evaluations = initial;
        while (evaluations < budget)
        {
            evaluations += size;
            size = LShadeSchedule.NextPopulationSize(initial, budget, evaluations, size);
            sizes.Add(size);
        }

        return (sizes, evaluations);
    }

    /// <summary>Σ x_j² for the CPU package.</summary>
    private sealed class CpuSphere : IFitnessFunctionEvaluator
    {
        public double Evaluate(ReadOnlySpan<double> genes)
        {
            var sum = 0.0;
            foreach (var gene in genes)
            {
                sum += gene * gene;
            }

            return sum;
        }

        public double Evaluate(int workerIndex, ReadOnlySpan<double> genes) => Evaluate(genes);
    }

    /// <summary>Σ x_j² for the GPU package.</summary>
    internal readonly struct GpuSphere : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes)
        {
            var sum = 0.0;
            for (var j = 0; j < genes.Length; j++)
            {
                sum += genes[j] * genes[j];
            }

            return sum;
        }
    }

    /// <summary>The CPU package's population size after each generation.</summary>
    private sealed class CpuSizes : Interfaces.IPopulationUpdatedHandler
    {
        public List<int> Sizes { get; } = [];

        public void Handle(Population population) => Sizes.Add(population.PopulationSize);
    }

    /// <summary>The GPU package's population size after each generation, and whether each reduced population was sorted.</summary>
    private sealed class GpuSizes(int initialSize) : IGpuPopulationUpdatedHandler
    {
        private int _previous = initialSize;

        public List<int> Sizes { get; } = [];

        public bool SortedAfterEachReduction { get; private set; } = true;

        public void Handle(GpuPopulationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.PopulationSize < _previous)
            {
                var fitness = snapshot.FitnessFunctionValues.Span;
                for (var i = 1; i < fitness.Length; i++)
                {
                    SortedAfterEachReduction &= FitnessOrder.KeyOf(fitness[i - 1]) <= FitnessOrder.KeyOf(fitness[i]);
                }
            }

            Sizes.Add(snapshot.PopulationSize);
            _previous = snapshot.PopulationSize;
        }
    }
}
