namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// Observes the population during a run. Called on the run's thread, every
/// <c>everyNGenerations</c> generations, with a host copy of the population; that copy is the only
/// per-generation transfer, and only a run with a handler makes it.
/// </summary>
public interface IGpuPopulationUpdatedHandler
{
    /// <summary>Receives the population after a generation. An exception thrown here faults the run's task.</summary>
    /// <param name="snapshot">A host copy of the population; the handler may keep it.</param>
    void Handle(GpuPopulationSnapshot snapshot);
}
