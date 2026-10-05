namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>The device state a scheme reads besides the population, for <see cref="HostStep.BuildSchemeTrial"/>.</summary>
/// <param name="BestIndex">The best individual's index.</param>
/// <param name="Ranking">The population's indices, best first; empty when the scheme reads none.</param>
/// <param name="Archive">The archive, individual-major; empty when there is none.</param>
/// <param name="ArchiveSize">The archive's size.</param>
internal sealed record SchemeState(int BestIndex, int[] Ranking, double[] Archive, int ArchiveSize)
{
    /// <summary>A state with a best index only.</summary>
    /// <param name="bestIndex">The best individual's index.</param>
    /// <returns>The state.</returns>
    public static SchemeState WithBest(int bestIndex) => new(bestIndex, [], [], 0);
}
