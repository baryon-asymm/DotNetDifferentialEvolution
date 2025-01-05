namespace DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;

/// <summary>
/// Interface for handling the event when a worker pass loop is done.
/// </summary>
public interface IWorkerPassLoopDoneHandler
{
    /// <summary>
    /// Handles the event when a worker pass loop is done.
    /// </summary>
    /// <param name="masterWorker">The worker controller that sent the event.</param>
    /// <param name="shouldTerminate">A boolean indicating whether the process should terminate.</param>
    public void Handle(WorkerController masterWorker, out bool shouldTerminate);
}
