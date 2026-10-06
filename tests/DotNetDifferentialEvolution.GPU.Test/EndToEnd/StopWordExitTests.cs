using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// ACCEPTANCE.md, check S18: with a stagnation rule, a run that ends at a generation limit or by a cancellation reads the
/// stop word first, and ends as at the stopping generation when the rule has fired. On ILGPU's CPU accelerator, jDE on a
/// stepped sphere, the stop word read every 1 000 generations, so that the limit and the cancellation fall after the stop
/// and before any read. The reference is the same run with the rule alone, which S12 holds to the CPU package's rule.
/// </summary>
[Trait("Category", "Integration")]
public class StopWordExitTests
{
    private const int MaxStreak = 5;
    private const int Seed = 3;
    private const int ReadInterval = 1000;

    private static readonly double[] Lower = [-3.0, -3.0, -3.0];
    private static readonly double[] Upper = [3.0, 3.0, 3.0];

    /// <summary>S18: a generation limit after the stop ends the run as the stop does.</summary>
    /// <returns>The runs.</returns>
    [Fact]
    public async Task ALimitAfterTheStopEndsTheRunAtTheStoppingGeneration()
    {
        var reference = await Reference().ConfigureAwait(true);

        using var limited = Builder(maxGenerations: reference.Generations + 3).Build();
        var result = await limited.RunAsync().ConfigureAwait(true);

        AssertSame(reference, result);
    }

    /// <summary>S18: a cancellation after the stop, before its read, completes the run as the stop does.</summary>
    /// <returns>The runs.</returns>
    [Fact]
    public async Task ACancellationAfterTheStopCompletesTheRunAtTheStoppingGeneration()
    {
        var reference = await Reference().ConfigureAwait(true);

        using var cancellation = new CancellationTokenSource();
        using var optimizer = Builder(maxGenerations: null).Build();
        optimizer.GenerationEnqueued = generation =>
        {
            if (generation == reference.Generations + 1)
            {
                cancellation.Cancel();
            }
        };
        var result = await optimizer.RunAsync(cancellation.Token).WaitAsync(HangGuard.Limit).ConfigureAwait(true);

        AssertSame(reference, result);
    }

    /// <summary>S18: a cancellation before the rule fires still cancels.</summary>
    /// <returns>The runs.</returns>
    [Fact]
    public async Task ACancellationBeforeTheStopStillCancels()
    {
        var reference = await Reference().ConfigureAwait(true);

        using var cancellation = new CancellationTokenSource();
        using var optimizer = Builder(maxGenerations: null).Build();
        optimizer.GenerationEnqueued = generation =>
        {
            if (generation == reference.Generations - 2)
            {
                cancellation.Cancel();
            }
        };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => optimizer.RunAsync(cancellation.Token).WaitAsync(HangGuard.Limit)).ConfigureAwait(true);
    }

    private static async Task<GpuOptimizationResult> Reference()
    {
        using var optimizer = Builder(maxGenerations: null).Build();
        var reference = await optimizer.RunAsync().WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        Assert.True(reference.Generations > MaxStreak + 2, $"the rule stopped at generation {reference.Generations}");
        Assert.True(reference.Generations + 3 < ReadInterval, $"the rule stopped at generation {reference.Generations}, too late for the read interval");
        return reference;
    }

    /// <summary>jDE with the stagnation rule; with a generation limit beside it when given, which only the internal builder allows.</summary>
    private static GpuBuilder<SteppedSphere> Builder(int? maxGenerations)
    {
        var builder = (GpuBuilder<SteppedSphere>)GpuDifferentialEvolutionBuilder
            .ForFunction(default(SteppedSphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(20)
            .WithJde()
            .WithStagnationLimit(MaxStreak, 0.0)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(Seed);
        if (maxGenerations is { } limit)
        {
            _ = builder.WithGenerationLimit(limit);
        }

        return builder.WithStopReadInterval(ReadInterval);
    }

    private static void AssertSame(GpuOptimizationResult expected, GpuOptimizationResult actual)
    {
        Assert.Equal(expected.Generations, actual.Generations);
        Assert.Equal(expected.EvaluationCount, actual.EvaluationCount);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.FitnessFunctionValue), BitConverter.DoubleToInt64Bits(actual.FitnessFunctionValue));
        Assert.Equal(expected.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits), actual.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits));
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
}
