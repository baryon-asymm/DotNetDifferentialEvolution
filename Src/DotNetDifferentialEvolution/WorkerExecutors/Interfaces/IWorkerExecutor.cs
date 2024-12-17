namespace DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

public interface IWorkerExecutor
{
    public void Execute(
        int workerId,
        Span<double> population,
        Span<double> populationFfValues,
        Span<double> bufferPopulation,
        Span<double> bufferPopulationFfValues,
        out int bestHandledIndividualIndex);
}
