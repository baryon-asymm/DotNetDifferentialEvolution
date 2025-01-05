namespace DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;

/// <summary>
/// Interface for executing an algorithm.
/// </summary>
public interface IAlgorithmExecutor
{
    /// <summary>
    /// Executes the algorithm.
    /// </summary>
    /// <param name="workerId">The index of the worker executing the algorithm.</param>
    /// <param name="bestHandledIndividualIndex">The index of the best handled individual.</param>
    public void Execute(
        int workerId,
        out int bestHandledIndividualIndex);
}
