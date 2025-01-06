using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;
using DotNetDifferentialEvolution.Models;

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
    
    private readonly TaskCompletionSource<Population> _resultPopulationTcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    
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
        _slaveWorkers = slaveWorkers;
        _context = context;
        _nextHandler = nextHandler;
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
        
            var bestIndividualIndex = GetBestIndividualIndex(masterWorker, _context.PopulationFfValues.Span);
            var population = _context.GetRepresentativePopulation(++_passLoopCounter, bestIndividualIndex);
        
            _context.PopulationUpdatedHandler?.Handle(population);
            
            shouldTerminate = _context.TerminationStrategy.ShouldTerminate(population);
            if (shouldTerminate)
            {
                StopAllWorkers();
                
                population.MoveCursorToBestIndividual();
                _resultPopulationTcs.SetResult(population);
            }
            else
            {
                PermitAllWorkersToStartPassLoop(masterWorker);
            }
        }
    }
    
    /// <summary>
    /// Gets the task that represents the result population.
    /// </summary>
    /// <returns>A task that represents the result population.</returns>
    public Task<Population> GetResultPopulationTask() => _resultPopulationTcs.Task;
    
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
            while (slaveWorker.IsPassLoopCompleted == false
                   && slaveWorker.HasException == false) ;
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
            if (slaveBestHandledIndividualFfValue < bestIndividualFfValue)
            {
                bestIndividualIndex = slaveBestHandledIndividualIndex;
                bestIndividualFfValue = slaveBestHandledIndividualFfValue;
            }
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
