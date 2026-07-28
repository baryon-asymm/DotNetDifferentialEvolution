using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;

/// <summary>
/// Handles the orchestration of worker controllers during the differential evolution algorithm execution.
/// </summary>
public class OrchestratorWorkerHandler : IWorkerPassLoopDoneHandler
{
    private int _passLoopCounter;
    
    private readonly ReadOnlyMemory<WorkerController> _slaveWorkers;
    
    private readonly ProblemContext _context;
    
    private readonly IWorkerPassLoopDoneHandler? _nextHandler;

    /// <summary>
    /// Scratch keys for the per-generation fitness ranking, or <see langword="null"/> when the
    /// configured mutation strategy never reads one.
    /// </summary>
    private readonly double[]? _fitnessSortKeys;

    private readonly TaskCompletionSource<Population> _resultPopulationTcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The token the run was started with. Read only on the orchestrator thread, at the barrier.
    /// </summary>
    private CancellationToken _cancellationToken = CancellationToken.None;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorWorkerHandler"/> class.
    /// </summary>
    /// <param name="slaveWorkers">The slave workers to be managed by the orchestrator.</param>
    /// <param name="context">The problem context containing population and other parameters.</param>
    /// <param name="nextHandler">The next handler in the chain of responsibility.</param>
    public OrchestratorWorkerHandler(
        ReadOnlyMemory<WorkerController> slaveWorkers,
        ProblemContext context,
        IWorkerPassLoopDoneHandler? nextHandler = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _slaveWorkers = slaveWorkers;
        _context = context;
        _nextHandler = nextHandler;

        _fitnessSortKeys = context.MutationRequirements.HasFlag(MutationRequirements.FitnessRanking)
            ? new double[context.PopulationSize]
            : null;
    }
    
    /// <summary>
    /// Handles the event when a worker pass loop is done.
    /// </summary>
    /// <param name="masterWorker">The worker controller that sent the event.</param>
    /// <param name="shouldTerminate">A boolean indicating whether the process should terminate.</param>
    public void Handle(
        WorkerController masterWorker,
        out bool shouldTerminate)
    {
        ArgumentNullException.ThrowIfNull(masterWorker);

        _nextHandler?.Handle(masterWorker, out _);
        
        WaitAllWorkersOrThemExceptions(
            masterWorker,
            out var hasException);
        
        if (hasException)
        {
            StopAllWorkers();
            
            var aggregateException = GetAggregateException(masterWorker);
            _resultPopulationTcs.SetException(aggregateException);
            
            shouldTerminate = true;
        }
        else
        {
            _context.SwapPopulations();

            // Count the evaluations performed during the generation that just finished.
            _context.EvaluationCount += _context.CurrentPopulationSize;

            // The fitness ranking is engine state, not adaptation state. A p-best strategy that
            // declared FitnessRanking gets a ranking of the population it is about to mutate no
            // matter which generation strategy is — or is not — installed; leaving it to the
            // adaptive strategies is what used to freeze it at generation 0 on hand-wired setups.
            // Ranking before AfterGeneration is also what gives L-SHADE's population reduction the
            // fresh ordering it picks survivors from.
            RebuildFitnessRanking();

            var generationStrategy = _context.GenerationStrategy;
            generationStrategy?.AfterGeneration(_context, _context.TrialRecords.Span);

            var bestIndividualIndex = generationStrategy is null
                ? GetBestIndividualIndex(masterWorker, _context.PopulationFfValues.Span)
                : FindBestIndividualIndex(_context.PopulationFfValues.Span, _context.CurrentPopulationSize);
            _context.BestIndividualIndex = bestIndividualIndex;

            var generationNumber = ++_passLoopCounter;

            // Memetic hook: refine the population in place every N generations. Improving the best
            // keeps it the best, so bestIndividualIndex stays valid; the refiner's own evaluations
            // are folded into EvaluationCount before the representative population is built, so the
            // observer and evaluation-budget termination both see the refined state.
            var localSearchRefiner = _context.LocalSearchRefiner;
            if (localSearchRefiner is not null && generationNumber % _context.LocalSearchInterval == 0)
                localSearchRefiner.Refine(_context, generationNumber);

            var population = _context.GetRepresentativePopulation(generationNumber, bestIndividualIndex);
        
            _context.PopulationUpdatedHandler?.Handle(population);
            
            shouldTerminate = _context.TerminationStrategy.ShouldTerminate(population);
            if (shouldTerminate)
            {
                StopAllWorkers();

                population.MoveCursorToBestIndividual();
                _resultPopulationTcs.SetResult(population);
            }
            else if (_cancellationToken.IsCancellationRequested)
            {
                // The barrier is the one moment the run is quiescent: every worker has finished
                // its stripe and none has been permitted to start the next. Stopping here leaves
                // the population in a consistent state and no thread mid-generation.
                StopAllWorkers();

                shouldTerminate = true;
                _resultPopulationTcs.SetCanceled(_cancellationToken);
            }
            else
            {
                PermitAllWorkersToStartPassLoop(masterWorker);
            }
        }
    }
    
    /// <summary>
    /// Re-ranks the active population into <see cref="ProblemContext.FitnessSortedIndices"/>, or
    /// does nothing when no configured strategy reads a ranking.
    /// </summary>
    private void RebuildFitnessRanking()
    {
        if (_fitnessSortKeys is null)
            return;

        PopulationSortHelper.SortIndicesByFitness(
            _context.FitnessSortedIndices.Span,
            _context.PopulationFfValues.Span,
            _context.CurrentPopulationSize,
            _fitnessSortKeys);
    }

    /// <summary>
    /// Gets the task that represents the result population.
    /// </summary>
    /// <returns>A task that represents the result population.</returns>
    public Task<Population> GetResultPopulationTask() => _resultPopulationTcs.Task;

    /// <summary>
    /// Sets the token the run may be abandoned through. Called once, before the workers start.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal void UseCancellationToken(
        CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Completes the result task as canceled without a generation having run.
    /// </summary>
    /// <param name="cancellationToken">The already-canceled token.</param>
    internal void CancelBeforeStart(
        CancellationToken cancellationToken)
    {
        _resultPopulationTcs.TrySetCanceled(cancellationToken);
    }
    
    /// <summary>
    /// Waits for all workers to complete their pass loops or encounter exceptions.
    /// </summary>
    /// <param name="masterWorker">The master worker controller.</param>
    /// <param name="hasException">A boolean indicating whether any worker encountered an exception.</param>
    private void WaitAllWorkersOrThemExceptions(
        WorkerController masterWorker,
        out bool hasException)
    {
        hasException = masterWorker.HasException;
        foreach (var slaveWorker in _slaveWorkers.Span)
        {
            // The master is itself a worker: it reaches this barrier after finishing its own
            // stripe and then waits on each slave in turn. A fresh SpinWait per slave so the
            // backoff starts from zero for every wait; SpinOnce(-1) keeps SpinWait's spin/yield
            // progression but never escalates to Thread.Sleep(1), which would add up to a
            // millisecond per generation. The condition order is unchanged: the volatile
            // IsPassLoopCompleted read is what makes the following HasException read fresh, and
            // HasException still breaks the wait promptly when a worker throws.
            var spinWait = new SpinWait();
            while (slaveWorker.IsPassLoopCompleted == false
                   && slaveWorker.HasException == false)
                spinWait.SpinOnce(sleep1Threshold: -1);
            hasException |= slaveWorker.HasException;
        }
    }
    
    /// <summary>
    /// Gets the aggregate exception from all workers.
    /// </summary>
    /// <param name="masterWorker">The master worker controller.</param>
    /// <returns>An aggregate exception containing all exceptions from the workers.</returns>
    private AggregateException GetAggregateException(
        WorkerController masterWorker)
    {
        var exceptions = new List<Exception>();
        if (masterWorker.HasException)
            exceptions.Add(masterWorker.Exception!);
        foreach (var slaveWorker in _slaveWorkers.Span)
        {
            if (slaveWorker.HasException)
                exceptions.Add(slaveWorker.Exception!);
        }
        
        return new AggregateException(exceptions);
    }
    
    /// <summary>
    /// Gets the index of the best individual in the population.
    /// </summary>
    /// <param name="masterWorker">The master worker controller.</param>
    /// <param name="populationFfValues">The fitness function values of the population.</param>
    /// <returns>The index of the best individual in the population.</returns>
    private int GetBestIndividualIndex(
        WorkerController masterWorker,
        Span<double> populationFfValues)
    {
        var slaveWorkers = _slaveWorkers.Span;
        
        var bestIndividualIndex = masterWorker.BestHandledIndividualIndex;
        var bestIndividualFfValue = populationFfValues[bestIndividualIndex];

        for (int i = 0; i < slaveWorkers.Length; i++)
        {
            var slaveBestHandledIndividualIndex = slaveWorkers[i].BestHandledIndividualIndex;
            var slaveBestHandledIndividualFfValue = populationFfValues[slaveBestHandledIndividualIndex];
            if (FitnessComparisonHelper.IsBetter(slaveBestHandledIndividualFfValue, bestIndividualFfValue))
            {
                bestIndividualIndex = slaveBestHandledIndividualIndex;
                bestIndividualFfValue = slaveBestHandledIndividualFfValue;
            }
        }
        
        return bestIndividualIndex;
    }
    
    /// <summary>
    /// Finds the index of the best (lowest fitness) individual by scanning the active
    /// population. Used when a generation strategy may have reordered or resized the population.
    /// A NaN individual never wins the scan; an all-NaN population still yields a valid index.
    /// </summary>
    /// <param name="populationFfValues">The fitness function values of the population.</param>
    /// <param name="currentPopulationSize">The number of active individuals.</param>
    /// <returns>The index of the best individual.</returns>
    private static int FindBestIndividualIndex(
        ReadOnlySpan<double> populationFfValues,
        int currentPopulationSize)
    {
        var bestIndividualIndex = 0;
        for (int i = 1; i < currentPopulationSize; i++)
        {
            if (FitnessComparisonHelper.IsBetter(populationFfValues[i], populationFfValues[bestIndividualIndex]))
                bestIndividualIndex = i;
        }

        return bestIndividualIndex;
    }

    /// <summary>
    /// Permits all workers to start their pass loops.
    /// </summary>
    /// <param name="masterWorker">The master worker controller.</param>
    private void PermitAllWorkersToStartPassLoop(
        WorkerController masterWorker)
    {
        masterWorker.PermitToPassLoop();
        foreach (var slaveWorker in _slaveWorkers.Span)
            slaveWorker.PermitToPassLoop();
    }
    
    /// <summary>
    /// Stops all workers.
    /// </summary>
    private void StopAllWorkers()
    {
        foreach (var slaveWorker in _slaveWorkers.Span)
            slaveWorker.Stop();
    }
}
