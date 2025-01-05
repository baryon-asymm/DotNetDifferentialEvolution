namespace DotNetDifferentialEvolution.Interfaces;

/// <summary>
/// Defines a strategy for generating an initial sample population 
/// in an optimization problem. The generated population serves as the starting 
/// point for the evolutionary process.
/// </summary>
public interface IPopulationSamplingMaker
{
    /// <summary>
    /// Fills the provided span with samples representing the initial population.
    /// The population is represented as a continuous sequence of genes, 
    /// where each subset of genes corresponds to an individual candidate solution.
    /// </summary>
    /// <param name="population">A preallocated <see cref="Span{Double}"/> representing 
    /// the population as a continuous sequence of genes. The span is divided into 
    /// segments, where each segment corresponds to the genes of a single individual.</param>
    public void SamplePopulation(
        Span<double> population);
}
