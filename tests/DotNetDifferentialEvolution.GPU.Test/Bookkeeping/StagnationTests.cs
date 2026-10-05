using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.TerminationStrategies;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// ACCEPTANCE.md, check S12: the stagnation limit.
/// <list type="bullet">
/// <item><see cref="StagnationRule.Apply"/> equals the CPU package's <see cref="StagnationStreakTerminationStrategy"/>,
/// step by step, on 100 scripted sequences of best values with <see cref="double.NaN"/>, ±∞, a threshold of 0 in some,
/// and a first value of <see cref="double.MinValue"/> in some.</item>
/// <item>A run whose objective stops improving ends at the generation the CPU rule gives for the best values its observer
/// saw, on ILGPU's CPU accelerator; and runs that read the stop word every generation and every 16 end with the same
/// generations, evaluations and result. An observer ends any run that reaches generation 10 000, so a rule that never
/// fires fails instead of hanging.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public class StagnationTests
{
    private const int SequenceCount = 100;
    private const int SequenceLength = 60;
    private const int CaseSeed = 20261012;

    /// <summary>A generation no run here reaches: a run still going there failed to stop, and its observer ends it.</summary>
    private const int GenerationGuard = 10000;

    private static readonly double[] Lower = [-3.0, -3.0, -3.0];
    private static readonly double[] Upper = [3.0, 3.0, 3.0];

    /// <summary>The rule equals the CPU strategy on every step of 100 sequences.</summary>
    [Fact]
    public void TheRuleEqualsTheCpuStrategy()
    {
        var random = new SeededRandomProvider(CaseSeed);
        var stops = 0;
        for (var sequence = 0; sequence < SequenceCount; sequence++)
        {
            var maxStreak = 1 + random.Next(8);
            var threshold = sequence % 4 == 0 ? 0.0 : random.NextDouble() * 0.1;
            var cpu = new StagnationStreakTerminationStrategy(maxStreak, threshold);
            var last = StagnationRule.InitialLastBest;
            var streak = 0;
            var best = 10.0;
            for (var step = 0; step < SequenceLength; step++)
            {
                best = (random.Next(12), step, sequence % 5) switch
                {
                    (_, 0, 0) => double.MinValue,
                    (0, _, _) => double.NaN,
                    (1, _, _) => double.PositiveInfinity,
                    (2, _, _) => double.NegativeInfinity,
                    (3 or 4 or 5 or 6, _, _) => best,
                    _ => best - random.NextDouble() * 0.2,
                };
                var gpuStops = StagnationRule.Apply(best, threshold, maxStreak, ref last, ref streak);
                var cpuStops = cpu.ShouldTerminate(new Population(new double[1], new[] { best }));

                var what = $"sequence {sequence}, step {step}, best {best}";
                Assert.True(cpuStops == gpuStops, $"{what}: CPU stops {cpuStops}, rule {gpuStops}");
                Assert.True(cpu.CurrentStagnationStreak == streak, $"{what}: CPU streak {cpu.CurrentStagnationStreak}, rule {streak}");
                Assert.True(
                    BitConverter.DoubleToInt64Bits(cpu.LastBestFitnessFunctionValue) == BitConverter.DoubleToInt64Bits(last),
                    $"{what}: CPU last {cpu.LastBestFitnessFunctionValue}, rule {last}");
                if (gpuStops)
                {
                    stops++;
                    break;
                }
            }
        }

        Assert.True(stops > 0, "no sequence stopped");
    }

    /// <summary>A run stops where the CPU rule stops on the best values it saw, and the read interval changes nothing.</summary>
    [Fact]
    public async Task ARunStopsWhereTheCpuRuleStops()
    {
        const int maxStreak = 5;
        var observed = new BestValues();
        using (var watched = Builder(stopReadInterval: RunSettings.DefaultStopReadInterval).WithPopulationUpdateHandler(observed).Build())
        {
            var watchedResult = await watched.RunAsync().ConfigureAwait(true);
            var cpu = new StagnationStreakTerminationStrategy(maxStreak, 0.0);
            var expected = 1 + observed.Values.FindIndex(best => cpu.ShouldTerminate(new Population(new double[1], new[] { best })));
            Assert.True(expected > maxStreak, $"the CPU rule stops at generation {expected}");
            Assert.Equal(expected, watchedResult.Generations);
            Assert.Equal(expected, observed.Values.Count);
        }

        using var everyGeneration = Builder(stopReadInterval: 1).WithPopulationUpdateHandler(new Guard(), GenerationGuard).Build();
        using var everySixteen = Builder(stopReadInterval: RunSettings.DefaultStopReadInterval).WithPopulationUpdateHandler(new Guard(), GenerationGuard).Build();
        var first = await everyGeneration.RunAsync().ConfigureAwait(true);
        var second = await everySixteen.RunAsync().ConfigureAwait(true);
        Assert.Equal(first.Generations, second.Generations);
        Assert.Equal(first.EvaluationCount, second.EvaluationCount);
        Assert.Equal(BitConverter.DoubleToInt64Bits(first.FitnessFunctionValue), BitConverter.DoubleToInt64Bits(second.FitnessFunctionValue));
        Assert.Equal(first.Genes.ToArray(), second.Genes.ToArray());
        Assert.True(first.Generations % RunSettings.DefaultStopReadInterval != 0, $"the run stopped at {first.Generations}, on a read of the interval");
    }

    private static GpuBuilder<SteppedSphere> Builder(int stopReadInterval)
    {
        var builder = (GpuBuilder<SteppedSphere>)GpuDifferentialEvolutionBuilder
            .ForFunction(default(SteppedSphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(20)
            .WithJde()
            .WithStagnationLimit(5, 0.0)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(3);
        return builder.WithStopReadInterval(stopReadInterval);
    }

    /// <summary>⌊4·Σ x_j²⌋: a sphere in steps, on which the best value soon stops moving.</summary>
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

            return Math.Floor(4.0 * sum);
        }
    }

    /// <summary>The best fitness of each generation the observer sees.</summary>
    private sealed class BestValues : IGpuPopulationUpdatedHandler
    {
        public List<double> Values { get; } = [];

        public void Handle(GpuPopulationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Values.Add(snapshot.FitnessFunctionValues.Span[BestPick.IndexOf(snapshot.FitnessFunctionValues.Span)]);
            Guard.ThrowAtTheGuard(snapshot.Generation);
        }
    }

    /// <summary>Ends a run that reached <see cref="GenerationGuard"/>: the observer's exception faults the run's task.</summary>
    private sealed class Guard : IGpuPopulationUpdatedHandler
    {
        public static void ThrowAtTheGuard(int generation)
        {
            if (generation >= GenerationGuard)
            {
                throw new InvalidOperationException($"the run reached generation {generation} without stopping");
            }
        }

        public void Handle(GpuPopulationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ThrowAtTheGuard(snapshot.Generation);
        }
    }
}
