using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

public class LimitGenerationNumberTerminationStrategy : ITerminationStrategy
{
    public int MaxGenerationNumber { get; init; }
    
    public LimitGenerationNumberTerminationStrategy(
        int maxGenerationNumber)
    {
        MaxGenerationNumber = maxGenerationNumber;
    }
    
    public bool ShouldTerminate(
        Population population)
    {
        return population.GenerationNumber >= MaxGenerationNumber;
    }
}
