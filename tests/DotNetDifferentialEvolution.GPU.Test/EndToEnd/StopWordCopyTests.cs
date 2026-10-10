namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A13 of the GPU package's Kernels ACCEPTANCE.md, on the CPU accelerator: with a stagnation limit, the control block
/// is copied to page-locked host memory every 16 generations without synchronising the accelerator, and the copy is read at the
/// next interval; only the observer and the end synchronise, so <c>PopulationTransfers</c> counts, in a run, as many
/// synchronising reads as the observer was called, plus the one at the end (which the observer's own read at the stopping
/// generation can be). The run ends where the rule fired whatever the interval (S12, S17, S18 stay green beside this).
/// </summary>
public class StopWordCopyTests
{
    private const int GenomeSize = 10;
    private const int Reference = 1000;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, GenomeSize)];
    private static readonly int[] Intervals = [1, 2, 7, 16, Reference];

    /// <summary>A13: with no observer, the one synchronising read of the run is the one at the end; the word is copied meanwhile.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ARunWithoutAnObserverReadsTheStopWordOnceAndCopiesItEverySixteenGenerations()
    {
        using var optimizer = Stage(interval: null, observer: null).Build();

        var result = await optimizer.RunAsync().WaitAsync(HangGuard.Limit).ConfigureAwait(true);

        Assert.True(result.Generations > 48, $"the run stopped after {result.Generations} generations, too few to tell");
        Assert.Equal(1, optimizer.StopReadCount);
        Assert.True(optimizer.StopCopyCount >= 2, $"the word was copied {optimizer.StopCopyCount} times");
        Assert.True(optimizer.StopCopyCount <= result.Generations / 16 + 1, $"the word was copied {optimizer.StopCopyCount} times in {result.Generations} generations");
        Assert.Equal(1, optimizer.PopulationDownloadCount);
    }

    /// <summary>A13: with an observer, the synchronising reads are its calls plus at most the one at the end.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ARunWithAnObserverReadsTheStopWordOncePerObserverCallAndAtMostOnceMore()
    {
        var observer = new RecordingObserver();
        using var optimizer = Stage(interval: null, observer).Build();

        var result = await optimizer.RunAsync().WaitAsync(HangGuard.Limit).ConfigureAwait(true);

        var calls = observer.Generations.Count;
        Assert.True(calls >= 2, $"the observer was called {calls} times");
        Assert.InRange(optimizer.StopReadCount, calls, calls + 1);
        Assert.True(optimizer.StopCopyCount >= 1, $"the word was copied {optimizer.StopCopyCount} times in {result.Generations} generations");
        Assert.Equal(calls + 1, optimizer.PopulationDownloadCount);
    }

    /// <summary>A13: the result is the same, bit for bit, whatever interval the copy is taken at, and whether it is read late or at the end.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task TheRunEndsAtTheGenerationTheRuleFiredWhateverTheInterval()
    {
        GpuOptimizationResult? first = null;
        foreach (var interval in Intervals)
        {
            using var optimizer = Stage(interval, observer: null).Build();
            var result = await optimizer.RunAsync().WaitAsync(HangGuard.Limit).ConfigureAwait(true);

            first ??= result;
            Assert.Equal(first.Generations, result.Generations);
            Assert.Equal(first.EvaluationCount, result.EvaluationCount);
            Assert.Equal(BitConverter.DoubleToInt64Bits(first.FitnessFunctionValue), BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue));
            Assert.Equal(first.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits), result.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    /// <summary>S17's configuration: SHADE on a stepped sphere, N = 50, a streak of 40, so that the rule fires after many intervals.</summary>
    private static GpuBuilder<SymmetryRunTests.SteppedSphere> Stage(int? interval, IGpuPopulationUpdatedHandler? observer)
    {
        var stage = GpuDifferentialEvolutionBuilder
            .ForFunction(default(SymmetryRunTests.SteppedSphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(50)
            .WithShade()
            .WithStagnationLimit(40, 0.0)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(7);
        var builder = (GpuBuilder<SymmetryRunTests.SteppedSphere>)(observer is null ? stage : stage.WithPopulationUpdateHandler(observer, 10));
        return interval is { } every ? builder.WithStopReadInterval(every) : builder;
    }
}
