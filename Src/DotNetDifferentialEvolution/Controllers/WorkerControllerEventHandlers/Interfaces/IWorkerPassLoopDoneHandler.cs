namespace DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;

public interface IWorkerPassLoopDoneHandler
{
    public void Handle(WorkerController workerController);
}
