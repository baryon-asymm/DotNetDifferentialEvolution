namespace DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;

public interface IAlgorithmExecutor
{
    public void Execute(
        int workerId,
        out int bestHandledIndividualIndex);
}
