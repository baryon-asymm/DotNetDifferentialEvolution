namespace DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

public interface IWorkerExecutor
{
    public void Execute(int workerId);
}