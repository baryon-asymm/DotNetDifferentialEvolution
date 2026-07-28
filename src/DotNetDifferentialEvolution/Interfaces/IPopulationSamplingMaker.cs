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

    /// <summary>
    /// Adopts the random source the engine supplies, so that a seeded run reproduces its initial
    /// population as well as its search.
    /// </summary>
    /// <param name="randomProvider">The random source to draw from.</param>
    /// <remarks>
    /// Called at most once, from <see cref="DifferentialEvolutionBuilder.Build"/>, and only when
    /// <see cref="DifferentialEvolutionBuilder.WithSeed"/> was used. Defaults to ignoring the
    /// provider, so an implementation with its own source of randomness — or none — is unaffected.
    /// </remarks>
    public void UseRandomProvider(
        BaseRandomProvider randomProvider)
    {
    }
}
