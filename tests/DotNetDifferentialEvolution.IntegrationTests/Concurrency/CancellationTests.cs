using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.Concurrency;

/// <summary>
/// A long run has to be abandonable. The generation barrier is the only point at which the engine
/// is quiescent — every worker has finished its stripe and none has started the next — so it is
/// where cancellation is observed: the workers are stopped there and the result task is completed
/// as canceled, rather than the caller being left to abandon a task whose threads keep running.
/// </summary>
[Trait("Category", "Integration")]
public class CancellationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task CancellingMidRunCompletesTheTaskAsCanceled(
        int workers)
    {
        using var cancellation = new CancellationTokenSource();
        var observer = new GenerationCountingObserver(cancelAtGeneration: 5, cancellation);

        using var de = Build(observer, workers);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => de.RunAsync(cancellation.Token).WaitAsync(Timeout));

        // Cancellation is observed at the barrier that follows the generation which requested it,
        // so exactly one more generation must not have started.
        Assert.Equal(5, observer.Generations);
    }

    [Fact]
    public async Task ATokenAlreadyCanceledStopsTheRunBeforeAnyGeneration()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var observer = new GenerationCountingObserver(cancelAtGeneration: null, cancellation);
        using var de = Build(observer, workers: 4);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => de.RunAsync(cancellation.Token).WaitAsync(Timeout));

        Assert.Equal(0, observer.Generations);
    }

    [Fact]
    public async Task ACanceledRunDisposesWithoutHanging()
    {
        using var cancellation = new CancellationTokenSource();
        var observer = new GenerationCountingObserver(cancelAtGeneration: 3, cancellation);

        var de = Build(observer, workers: 4);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => de.RunAsync(cancellation.Token).WaitAsync(Timeout));
        }
        finally
        {
            // Dispose stops and joins every worker thread; if a worker were still spinning past
            // the barrier this would not return.
            await Task.Run(de.Dispose).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task AnUncanceledRunIsUnaffected()
    {
        using var cancellation = new CancellationTokenSource();
        var observer = new GenerationCountingObserver(cancelAtGeneration: null, cancellation);

        using var de = Build(observer, workers: 4);

        var result = await de.RunAsync(cancellation.Token).WaitAsync(Timeout);

        Assert.Equal(Generations, observer.Generations);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RunAsyncWithoutATokenStillWorks()
    {
        var observer = new GenerationCountingObserver(cancelAtGeneration: null, cancellationSource: null);

        using var de = Build(observer, workers: 4);

        var result = await de.RunAsync().WaitAsync(Timeout);

        Assert.Equal(Generations, observer.Generations);
        Assert.NotNull(result);
    }

    private const int Generations = 50;

    private static DifferentialEvolution Build(
        IPopulationUpdatedHandler observer,
        int workers)
    {
        var evaluator = new SphereEvaluator(dimension: 6);

        return DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(40)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.6, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(Generations))
            .UseProcessors(workers)
            .WithPopulationUpdateHandler(observer)
            .Build();
    }

    /// <summary>
    /// Counts generations and, optionally, cancels from inside one. Cancelling here rather than
    /// from a timer makes the test deterministic: the handler runs on the orchestrator thread just
    /// before the termination check, so the cancellation is guaranteed to be seen at that barrier.
    /// </summary>
    private sealed class GenerationCountingObserver : IPopulationUpdatedHandler
    {
        private readonly int? _cancelAtGeneration;
        private readonly CancellationTokenSource? _cancellationSource;

        public GenerationCountingObserver(
            int? cancelAtGeneration,
            CancellationTokenSource? cancellationSource)
        {
            _cancelAtGeneration = cancelAtGeneration;
            _cancellationSource = cancellationSource;
        }

        public int Generations { get; private set; }

        public void Handle(
            Population population)
        {
            ArgumentNullException.ThrowIfNull(population);

            Generations = population.GenerationNumber;

            if (Generations == _cancelAtGeneration)
                _cancellationSource?.Cancel();
        }
    }
}
