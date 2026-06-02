using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Interfaces;

/// <summary>
/// Defines a mechanism for handling updates to the population during 
/// the evolutionary process. This interface allows for tracking, analyzing, 
/// or logging changes to the population after each generation.
/// </summary>
public interface IPopulationUpdatedHandler
{
    /// <summary>
    /// Processes the updated population at the end of a generation.
    /// This method is called after the population has been modified, allowing 
    /// for actions such as monitoring progress, logging statistics, or implementing custom logic.
    /// </summary>
    /// <param name="population">The updated <see cref="Population"/> object, 
    /// representing the state of the population after the current generation.</param>
    public void Handle(
        Population population);
}
