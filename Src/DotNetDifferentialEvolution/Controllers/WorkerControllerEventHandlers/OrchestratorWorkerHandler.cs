using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;

public class OrchestratorWorkerHandler : IWorkerPassLoopDoneHandler
{
    private int _passLoopCounter;
    
    private readonly ReadOnlyMemory<WorkerController> _slaveWorkers;
    
    private readonly ProblemContext _context;
    
    private readonly IWorkerPassLoopDoneHandler? _nextHandler;
    
    private readonly TaskCompletionSource<Population> _resultPopulationTcs = new();
    
    public OrchestratorWorkerHandler(
        ReadOnlyMemory<WorkerController> slaveWorkers,
        ProblemContext context,
        IWorkerPassLoopDoneHandler? nextHandler = null)
    {
        _slaveWorkers = slaveWorkers;
        _context = context;
        _nextHandler = nextHandler;
    }
    
    public void Handle(
        WorkerController sender,
        out bool shouldTerminate)
    {
        _nextHandler?.Handle(sender, out _);
        
        WaitAllWorkersOrThemExceptions(
            sender,
            out var hasException);
        
        if (hasException)
        {
            StopAllWorkers();
            
            var aggregateException = GetAggregateException(sender);
            _resultPopulationTcs.SetException(aggregateException);
            
            shouldTerminate = true;
        }
        else
        {
            _context.SwapPopulations();
        
            var bestIndividualIndex = GetBestIndividualIndex(sender, _context.PopulationFfValues.Span);
            var population = _context.GetRepresentativePopulation(++_passLoopCounter, bestIndividualIndex);
        
            _context.PopulationUpdatedHandler?.Handle(population);
            
            shouldTerminate = _context.TerminationStrategy.ShouldTerminate(population);
            if (shouldTerminate)
            {
                StopAllWorkers();
                _resultPopulationTcs.SetResult(population);
            }
            else
            {
                PermitAllWorkersToStartPassLoop(sender);
            }
        }
    }
    
    public Task<Population> GetResultPopulationTask() => _resultPopulationTcs.Task;
    
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
    
    private void PermitAllWorkersToStartPassLoop(
        WorkerController masterWorker)
    {
        masterWorker.PermitToPassLoop();
        foreach (var slaveWorker in _slaveWorkers.Span)
            slaveWorker.PermitToPassLoop();
    }
    
    private void StopAllWorkers()
    {
        foreach (var slaveWorker in _slaveWorkers.Span)
            slaveWorker.Stop();
    }
}
