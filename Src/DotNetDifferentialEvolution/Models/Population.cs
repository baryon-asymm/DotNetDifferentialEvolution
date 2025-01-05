using DotNetDifferentialEvolution.Models.Interfaces;

namespace DotNetDifferentialEvolution.Models;

/// <summary>
/// Represents a population of individuals in the differential evolution algorithm.
/// </summary>
public class Population : IIndividualCursorUpdater
{
    private readonly ReadOnlyMemory<double> _genes;
    private readonly ReadOnlyMemory<double> _fitnessFunctionValues;
    
    /// <summary>
    /// Gets or sets the individual cursor for the population.
    /// </summary>
    public IndividualCursor IndividualCursor { get; init; }
    
    /// <summary>
    /// Gets or sets the generation number of the population.
    /// </summary>
    public int GenerationNumber { get; set; }

    /// <summary>
    /// Gets the size of the genome for each individual in the population.
    /// </summary>
    public int GenomeSize => _genes.Length / PopulationSize;

    /// <summary>
    /// Gets the size of the population.
    /// </summary>
    public int PopulationSize => _fitnessFunctionValues.Length;

    /// <summary>
    /// Gets or sets the index of the best individual in the population.
    /// </summary>
    public int BestIndividualIndex { get; set; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Population"/> class.
    /// </summary>
    /// <param name="genes">The genes of the population.</param>
    /// <param name="fitnessFunctionValues">The fitness function values of the population.</param>
    public Population(
        ReadOnlyMemory<double> genes,
        ReadOnlyMemory<double> fitnessFunctionValues)
    {
        _genes = genes;
        _fitnessFunctionValues = fitnessFunctionValues;
        
        IndividualCursor = new IndividualCursor(
            double.MaxValue,
            _genes.Slice(0, GenomeSize));
    }
    
    /// <summary>
    /// Moves the individual cursor to the specified individual index.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to move the cursor to.</param>
    public void MoveCursorTo(
        int individualIndex)
    {
        IndividualCursor.AcceptUpdater(
            individualIndex,
            this);
    }
    
    /// <summary>
    /// Moves the individual cursor to the best individual in the population.
    /// </summary>
    public void MoveCursorToBestIndividual()
    {
        MoveCursorTo(BestIndividualIndex);
    }

    /// <summary>
    /// Updates the individual at the specified index.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to be updated.</param>
    /// <param name="fitnessFunctionValue">The fitness function value of the individual.</param>
    /// <param name="genes">The genes of the individual.</param>
    public void Update(
        int individualIndex,
        ref double fitnessFunctionValue,
        ref ReadOnlyMemory<double> genes)
    {
        fitnessFunctionValue = _fitnessFunctionValues.Span[individualIndex];
        genes = _genes.Slice(individualIndex * GenomeSize, GenomeSize);
    }
}
