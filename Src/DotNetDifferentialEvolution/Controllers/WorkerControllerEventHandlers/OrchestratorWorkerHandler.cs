using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;

public class OrchestratorWorkerHandler : IWorkerPassLoopDoneHandler
{
    private int _passLoopCounter;
    
    private readonly ReadOnlyMemory<WorkerController> _otherWorkerControllers;
    
    private readonly ProblemContext _context;
    
    private readonly IWorkerPassLoopDoneHandler? _nextHandler;
    
    private readonly TaskCompletionSource<Population> _resultPopulationTcs = new();
    
    public OrchestratorWorkerHandler(
        ReadOnlyMemory<WorkerController> otherWorkerControllers,
        ProblemContext context,
        IWorkerPassLoopDoneHandler? nextHandler = null)
    {
        _otherWorkerControllers = otherWorkerControllers;
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
        
            var bestIndividualIndex = GetBestIndividualIndex(sender);
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
    
    public Task<Population> GetResultPopulationTask()
    {
        return _resultPopulationTcs.Task;
    }
    
    private void WaitAllWorkersOrThemExceptions(
        WorkerController workerController,
        out bool hasException)
    {
        hasException = workerController.HasException;
        var otherWorkerControllers = _otherWorkerControllers.Span;
        for (int i = 0; i < otherWorkerControllers.Length; i++)
        {
            while (otherWorkerControllers[i].IsPassLoopCompleted == false
                   && otherWorkerControllers[i].HasException == false) ;
            hasException |= otherWorkerControllers[i].HasException;
        }
    }
    
    private AggregateException GetAggregateException(
        WorkerController workerController)
    {
        var exceptions = new List<Exception>();
        if (workerController.HasException)
            exceptions.Add(workerController.Exception!);
        var otherWorkerControllers = _otherWorkerControllers.Span;
        for (int i = 0; i < otherWorkerControllers.Length; i++)
        {
            if (otherWorkerControllers[i].HasException)
                exceptions.Add(otherWorkerControllers[i].Exception!);
        }
        
        return new AggregateException(exceptions);
    }
    
    private int GetBestIndividualIndex(
        WorkerController workerController)
    {
        var otherWorkerControllers = _otherWorkerControllers.Span;
        var trialPopulationFfValues = _context.TrialPopulationFfValues.Span;
        
        var bestIndividualIndex = workerController.BestHandledIndividualIndex;
        var bestIndividualFfValue = trialPopulationFfValues[bestIndividualIndex];

        for (int i = 0; i < otherWorkerControllers.Length; i++)
        {
            var otherBestHandledIndividualIndex = otherWorkerControllers[i].BestHandledIndividualIndex;
            var otherBestHandledIndividualFfValue = trialPopulationFfValues[otherBestHandledIndividualIndex];
            if (otherBestHandledIndividualFfValue < bestIndividualFfValue)
            {
                bestIndividualIndex = otherBestHandledIndividualIndex;
                bestIndividualFfValue = otherBestHandledIndividualFfValue;
            }
        }
        
        return bestIndividualIndex;
    }
    
    private void PermitAllWorkersToStartPassLoop(
        WorkerController workerController)
    {
        workerController.PermitToPassLoop();
        var otherWorkerControllers = _otherWorkerControllers.Span;
        for (int i = 0; i < otherWorkerControllers.Length; i++)
            otherWorkerControllers[i].PermitToPassLoop();
    }
    
    private void StopAllWorkers()
    {
        var otherWorkerControllers = _otherWorkerControllers.Span;
        for (int i = 0; i < otherWorkerControllers.Length; i++)
            otherWorkerControllers[i].Stop();
    }
}
