using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.TerminationStrategies;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Checks S13, S14 and S17 of the GPU package's ACCEPTANCE.md, over the nine configurations of the symmetry with the CPU
/// package (the five fixed schemes with F = 0.5, CR = 0.9; jDE, JADE, SHADE and L-SHADE with their defaults).
/// <list type="bullet">
/// <item>S13, the positive control: seed 1, Sphere D = 10 in [−5, 5], 2·10⁵ evaluations, N = 100 (L-SHADE N_init 180),
/// each reaches f ≤ 1e-6, and the CPU package with the same configuration does too. On the CPU accelerator in CI, on
/// CUDA under <c>Gpu</c>.</item>
/// <item>S14: each configuration, and the stagnation limit, run twice with one seed on one device give bit-identical
/// results and observer snapshots.</item>
/// <item>S17: with a stagnation limit the stop word is read at most ⌈G/16⌉ + (observer calls) + 1 times and the population
/// downloaded only for the observer and at the end; without one it is never read.</item>
/// </list>
/// </summary>
/// <param name="output">Receives the value each run reached.</param>
public class SymmetryRunTests(ITestOutputHelper output)
{
    private const int GenomeSize = 10;
    private const long Budget = 200_000;
    private const double Target = 1e-6;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, GenomeSize)];

    /// <summary>The nine configurations, by the builder method's name.</summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Configurations() =>
    [
        nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithJde),
        nameof(IGpuMutationStrategyRequired<>.WithJade),
        nameof(IGpuMutationStrategyRequired<>.WithShade),
        nameof(IGpuMutationStrategyRequired<>.WithLShade),
    ];

    /// <summary>S13 on the CPU accelerator: the configuration reaches 1e-6, as the CPU package does.</summary>
    /// <param name="configuration">The builder method.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Configurations))]
    public Task EachConfigurationConvergesOnTheCpuAccelerator(string configuration) => AssertConverges(configuration, GpuDevice.Cpu);

    /// <summary>S13 on CUDA.</summary>
    /// <param name="configuration">The builder method.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(Configurations))]
    public Task EachConfigurationConvergesOnCuda(string configuration) => AssertConverges(configuration, GpuDevice.Cuda);

    /// <summary>S14 on the CPU accelerator: two runs with one seed are bit-identical, snapshots included.</summary>
    /// <param name="configuration">The builder method.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Configurations))]
    public Task EachConfigurationIsReproducibleOnTheCpuAccelerator(string configuration) => AssertReproducible(configuration, GpuDevice.Cpu);

    /// <summary>S14 on CUDA.</summary>
    /// <param name="configuration">The builder method.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(Configurations))]
    public Task EachConfigurationIsReproducibleOnCuda(string configuration) => AssertReproducible(configuration, GpuDevice.Cuda);

    /// <summary>S14 for the stagnation limit, on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TheStagnationLimitIsReproducible()
    {
        var (first, _, _) = await StagnationRun(observer: null).ConfigureAwait(true);
        var (second, _, _) = await StagnationRun(observer: null).ConfigureAwait(true);
        AssertSame(first, second, "stagnation");
    }

    /// <summary>S17: the stop word's reads and the population's downloads, with and without a stagnation limit.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TheStopWordIsReadOnlyEverySixteenGenerationsAndForTheObserver()
    {
        var (quiet, quietReads, quietDownloads) = await StagnationRun(observer: null).ConfigureAwait(true);
        var generations = quiet.Generations;
        Assert.True(generations > 32, $"the run stopped after {generations} generations, too few to tell");
        Assert.InRange(quietReads, 1, (generations + 15) / 16 + 1);
        Assert.Equal(1, quietDownloads);

        var observer = new Snapshots();
        var (_, watchedReads, watchedDownloads) = await StagnationRun(observer, everyNGenerations: 10).ConfigureAwait(true);
        var calls = observer.Taken.Count;
        Assert.InRange(watchedReads, 1, (generations + 15) / 16 + calls + 1);
        Assert.Equal(calls + 1, watchedDownloads);

        using var limited = Build(nameof(IGpuMutationStrategyRequired<>.WithJde), GpuDevice.Cpu, observer: null, budget: 2000);
        _ = await limited.RunAsync().ConfigureAwait(true);
        Assert.Equal(0, limited.StopReadCount);
    }

    private static GpuDifferentialEvolution Build(string configuration, GpuDevice device, IGpuPopulationUpdatedHandler? observer, long budget = Budget)
    {
        var stage = GpuDifferentialEvolutionBuilder
            .ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(configuration == nameof(IGpuMutationStrategyRequired<>.WithLShade) ? 180 : 100);
        var limit = Scheme(stage, configuration, budget).WithEvaluationLimit(budget).OnDevice(device).WithSeed(1);
        return (observer is null ? limit : limit.WithPopulationUpdateHandler(observer, 50)).Build();
    }

    private static IGpuTerminationConditionRequired<Sphere> Scheme(IGpuMutationStrategyRequired<Sphere> stage, string configuration, long budget) => configuration switch
    {
        nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy) => stage.WithDefaultMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy) => stage.WithBestMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy) => stage.WithCurrentToBestMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy) => stage.WithRandTwoMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy) => stage.WithBestTwoMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithJde) => stage.WithJde(),
        nameof(IGpuMutationStrategyRequired<>.WithJade) => stage.WithJade(),
        nameof(IGpuMutationStrategyRequired<>.WithShade) => stage.WithShade(),
        nameof(IGpuMutationStrategyRequired<>.WithLShade) => stage.WithLShade(budget),
        _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration, "Not a configuration."),
    };

    private static async Task<double> CpuPackage(string configuration)
    {
        var stage = DifferentialEvolutionBuilder
            .ForFunction(new CpuSphere())
            .WithBounds(Lower, Upper)
            .WithPopulationSize(configuration == nameof(IGpuMutationStrategyRequired<>.WithLShade) ? 180 : 100)
            .WithUniformPopulationSampling();
        var limit = new LimitEvaluationNumberTerminationStrategy(Budget);
        var terminated = configuration switch
        {
            nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy) => stage.WithDefaultMutationStrategy(0.5, 0.9).WithDefaultSelectionStrategy().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy) => stage.WithBestMutationStrategy(0.5, 0.9).WithDefaultSelectionStrategy().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy) => stage.WithCurrentToBestMutationStrategy(0.5, 0.9).WithDefaultSelectionStrategy().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy) => stage.WithRandTwoMutationStrategy(0.5, 0.9).WithDefaultSelectionStrategy().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy) => stage.WithBestTwoMutationStrategy(0.5, 0.9).WithDefaultSelectionStrategy().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithJde) => stage.WithJde().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithJade) => stage.WithJade().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithShade) => stage.WithShade().WithTerminationCondition(limit),
            nameof(IGpuMutationStrategyRequired<>.WithLShade) => stage.WithLShade(Budget).WithTerminationCondition(limit),
            _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration, "Not a configuration."),
        };
        using var cpu = terminated.UseProcessors(1).WithSeed(1).Build();
        var population = await cpu.RunAsync().ConfigureAwait(true);
        population.MoveCursorToBestIndividual();
        return population.IndividualCursor.FitnessFunctionValue;
    }

    private static void AssertSame(GpuOptimizationResult first, GpuOptimizationResult second, string what)
    {
        Assert.True(first.Generations == second.Generations, $"{what}: generations {first.Generations} and {second.Generations}");
        Assert.Equal(first.EvaluationCount, second.EvaluationCount);
        Assert.Equal(BitConverter.DoubleToInt64Bits(first.FitnessFunctionValue), BitConverter.DoubleToInt64Bits(second.FitnessFunctionValue));
        Assert.Equal(first.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits), second.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    private static async Task<(GpuOptimizationResult Result, int StopReads, int Downloads)> StagnationRun(IGpuPopulationUpdatedHandler? observer, int everyNGenerations = 1)
    {
        var limit = GpuDifferentialEvolutionBuilder
            .ForFunction(default(SteppedSphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(50)
            .WithShade()
            .WithStagnationLimit(40, 0.0)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(7);
        using var optimizer = (observer is null ? limit : limit.WithPopulationUpdateHandler(observer, everyNGenerations)).Build();
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        return (result, optimizer.StopReadCount, optimizer.PopulationDownloadCount);
    }

    private async Task AssertConverges(string configuration, GpuDevice device)
    {
        using var optimizer = Build(configuration, device, observer: null);
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        var cpu = await CpuPackage(configuration).ConfigureAwait(true);
        output.WriteLine($"{configuration} on {device}: {result.FitnessFunctionValue:R} after {result.Generations} generations; the CPU package {cpu:R}");

        Assert.True(cpu <= Target, $"the positive control failed: the CPU package's {configuration} reached {cpu:R}");
        Assert.True(result.FitnessFunctionValue <= Target, $"{configuration} on {device} reached {result.FitnessFunctionValue:R}");
    }

    private async Task AssertReproducible(string configuration, GpuDevice device)
    {
        var firstSnapshots = new Snapshots();
        var secondSnapshots = new Snapshots();
        GpuOptimizationResult first;
        GpuOptimizationResult second;
        using (var optimizer = Build(configuration, device, firstSnapshots, budget: 20_000))
        {
            first = await optimizer.RunAsync().ConfigureAwait(true);
        }

        using (var optimizer = Build(configuration, device, secondSnapshots, budget: 20_000))
        {
            second = await optimizer.RunAsync().ConfigureAwait(true);
        }

        AssertSame(first, second, configuration);
        Assert.Equal(firstSnapshots.Taken.Count, secondSnapshots.Taken.Count);
        Assert.True(firstSnapshots.Taken.Count > 1, $"{configuration}: {firstSnapshots.Taken.Count} snapshots");
        for (var k = 0; k < firstSnapshots.Taken.Count; k++)
        {
            Assert.Equal(firstSnapshots.Taken[k], secondSnapshots.Taken[k]);
        }

        output.WriteLine($"{configuration} on {device}: {first.FitnessFunctionValue:R} twice, {firstSnapshots.Taken.Count} snapshots alike");
    }

    /// <summary>Σ x_j² for the GPU package.</summary>
    internal readonly struct Sphere : IGpuFitnessFunction
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

    /// <summary>⌊Σ x_j²⌋: a sphere in steps, on which the best value soon stops moving.</summary>
    internal readonly struct SteppedSphere : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes)
        {
            var sum = 0.0;
            for (var j = 0; j < genes.Length; j++)
            {
                sum += genes[j] * genes[j];
            }

            return Math.Floor(sum);
        }
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

    /// <summary>Every snapshot as text of its bits, for exact comparison.</summary>
    private sealed class Snapshots : IGpuPopulationUpdatedHandler
    {
        public List<string> Taken { get; } = [];

        public void Handle(GpuPopulationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var bits = snapshot.Genes.ToArray().Concat(snapshot.FitnessFunctionValues.ToArray()).Select(BitConverter.DoubleToInt64Bits);
            Taken.Add($"{snapshot.Generation}/{snapshot.EvaluationCount}/{snapshot.PopulationSize}:" + string.Join(",", bits));
        }
    }
}
