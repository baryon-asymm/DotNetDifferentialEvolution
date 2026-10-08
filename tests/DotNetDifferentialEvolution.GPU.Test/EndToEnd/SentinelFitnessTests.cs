using DotNetDifferentialEvolution.GPU.Objectives;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md, check S19, the run: SHADE and L-SHADE on an objective that scores every infeasible point
/// <see cref="double.MaxValue"/> (the usual sentinel) and a <see cref="double.NaN"/> gene as 0. Improvements over a sentinel
/// parent are finite and near <see cref="double.MaxValue"/>, and two of them sum to +∞ unless the weights are scaled first;
/// the memory then holds <see cref="double.NaN"/>, F and CR come out <see cref="double.NaN"/> and the mutants are all
/// <see cref="double.NaN"/>, which this objective scores 0, the best value there is. So a run whose memory went
/// <see cref="double.NaN"/> ends with a <see cref="double.NaN"/> gene in its best individual. On the CPU accelerator in CI
/// (population 100, 20 000 evaluations, seed 12345), on CUDA under <c>Gpu</c> (L-SHADE, population 16 384, 32 genes,
/// 5 000 000 evaluations, seed 20261007).
/// </summary>
/// <param name="output">Receives the value each run reached.</param>
public class SentinelFitnessTests(ITestOutputHelper output)
{
    private const int PopulationSize = 100;
    private const int GenomeSize = 8;
    private const long Budget = 20_000;
    private const int Seed = 12345;

    /// <summary>S19 on the CPU accelerator: SHADE ends with no <see cref="double.NaN"/> gene in its best individual.</summary>
    /// <returns>The run.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ShadeEndsWithNoNaNGeneWhenInfeasiblePointsScoreMaxValue()
    {
        using var optimizer = Stage(PopulationSize, GenomeSize)
            .WithShade()
            .WithEvaluationLimit(Budget)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(Seed)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        AssertSound(result, "SHADE on the CPU accelerator");
    }

    /// <summary>S19 on the CPU accelerator: L-SHADE ends with no <see cref="double.NaN"/> gene in its best individual.</summary>
    /// <returns>The run.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LShadeEndsWithNoNaNGeneWhenInfeasiblePointsScoreMaxValue()
    {
        using var optimizer = Stage(PopulationSize, GenomeSize)
            .WithLShade(Budget)
            .WithEvaluationLimit(Budget)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(Seed)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        AssertSound(result, "L-SHADE on the CPU accelerator");
    }

    /// <summary>S19 on CUDA, the orchestrator's to run: L-SHADE, population 16 384, 32 genes, 5 000 000 evaluations, seed 20261007.</summary>
    /// <returns>The run.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public async Task LShadeEndsWithNoNaNGeneWhenInfeasiblePointsScoreMaxValueOnCuda()
    {
        const long cudaBudget = 5_000_000;
        using var optimizer = Stage(16_384, 32)
            .WithLShade(cudaBudget)
            .WithEvaluationLimit(cudaBudget)
            .OnDevice(GpuDevice.Cuda)
            .WithSeed(20261007)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        AssertSound(result, "L-SHADE on CUDA");
    }

    private static IGpuMutationStrategyRequired<SentinelSphere> Stage(int populationSize, int genomeSize) =>
        GpuDifferentialEvolutionBuilder
            .ForFunction(default(SentinelSphere))
            .WithBounds(Enumerable.Repeat(-5.0, genomeSize).ToArray(), Enumerable.Repeat(5.0, genomeSize).ToArray())
            .WithPopulationSize(populationSize);

    private void AssertSound(GpuOptimizationResult result, string what)
    {
        output.WriteLine($"{what}: {result.FitnessFunctionValue:R} after {result.Generations} generations");
        var genes = result.Genes.ToArray();
        Assert.Equal(-1, Array.FindIndex(genes, double.IsNaN));
        Assert.True(double.IsFinite(result.FitnessFunctionValue), $"{what}: the best fitness is {result.FitnessFunctionValue:R}");
    }

    /// <summary>
    /// A sphere that is feasible only for x₀ &lt; −4 and scores <see cref="double.MaxValue"/> everywhere else; a
    /// <see cref="double.NaN"/> gene scores 0.
    /// </summary>
    internal readonly struct SentinelSphere : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes)
        {
            var sum = 0.0;
            for (var j = 0; j < genes.Length; j++)
            {
                if (double.IsNaN(genes[j]))
                {
                    return 0.0;
                }

                sum += genes[j] * genes[j];
            }

            return genes[0] < -4.0 ? sum : double.MaxValue;
        }
    }
}
