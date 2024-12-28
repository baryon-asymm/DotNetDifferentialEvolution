using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

public interface ITerminationStrategy
{
    public bool ShouldTerminate(Population population);
}
