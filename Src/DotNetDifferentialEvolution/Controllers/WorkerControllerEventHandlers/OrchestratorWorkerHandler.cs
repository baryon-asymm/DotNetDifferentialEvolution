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
        WorkerController workerController)
    {
        _nextHandler?.Handle(workerController);
        
        WaitUntilAllWorkersCompletePassLoop();

        _context.SwapPopulations();
        
        var bestIndividualIndex = GetBestIndividualIndex(workerController);
        var population = _context.GetRepresentativePopulation(++_passLoopCounter, bestIndividualIndex);
        
        _context.PopulationUpdatedHandler?.Handle(population);
        var shouldTerminate = _context.TerminationStrategy.ShouldTerminate(population);

        if (shouldTerminate)
        {
            StopAllWorkers(workerController);
            _resultPopulationTcs.SetResult(population);
        }
        else
        {
            PermitAllWorkersToStartPassLoop(workerController);
        }
    }
    
    public Task<Population> GetResultPopulationTask()
    {
        return _resultPopulationTcs.Task;
    }
    
    private void WaitUntilAllWorkersCompletePassLoop()
    {
        var otherWorkerControllers = _otherWorkerControllers.Span;
        for (int i = 0; i < otherWorkerControllers.Length; i++)
        {
            while (otherWorkerControllers[i].IsPassLoopCompleted == false) ;
        }
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
    
    private void StopAllWorkers(
        WorkerController workerController)
    {
        workerController.Stop();
        var otherWorkerControllers = _otherWorkerControllers.Span;
        for (int i = 0; i < otherWorkerControllers.Length; i++)
            otherWorkerControllers[i].Stop(waitUntilStopped: true);
    }
}
