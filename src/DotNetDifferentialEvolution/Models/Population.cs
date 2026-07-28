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
    /// <remarks>
    /// Derived from <see cref="Capacity"/>, never from <see cref="PopulationSize"/>: the gene
    /// buffer is allocated once and is not resized when the active population shrinks, so
    /// dividing by the active size would make the genome appear to grow.
    /// </remarks>
    public int GenomeSize => _genes.Length / Capacity;

    /// <summary>
    /// Gets the number of individuals currently taking part in the search. Indices
    /// <c>0 .. PopulationSize - 1</c> are the live ones; everything above is a leftover of an
    /// earlier generation.
    /// </summary>
    /// <remarks>
    /// This equals <see cref="Capacity"/> unless a strategy reduces the population — L-SHADE's
    /// Linear Population Size Reduction shrinks it toward
    /// <see cref="Algorithms.Lshade.LShadeStrategy.MinimumPopulationSize"/> as the evaluation
    /// budget is consumed, without reallocating the buffers.
    /// </remarks>
    public int PopulationSize { get; internal set; }

    /// <summary>
    /// Gets the number of individuals the underlying buffers were allocated for — the population
    /// size the run started with.
    /// </summary>
    public int Capacity => _fitnessFunctionValues.Length;

    /// <summary>
    /// Gets or sets the index of the best individual in the population.
    /// </summary>
    public int BestIndividualIndex { get; set; }

    /// <summary>
    /// Gets or sets the total number of fitness-function evaluations performed so far.
    /// </summary>
    public long EvaluationCount { get; set; }
    
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

        // A population starts fully active; the engine narrows this when a strategy reduces it.
        PopulationSize = fitnessFunctionValues.Length;

        IndividualCursor = new IndividualCursor(
            double.MaxValue,
            _genes.Slice(0, GenomeSize));
    }

    /// <summary>
    /// Moves the individual cursor to the specified individual index.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to move the cursor to.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="individualIndex"/> is outside the active population. Reading past
    /// <see cref="PopulationSize"/> would hand back an individual the run already discarded.
    /// </exception>
    public void MoveCursorTo(
        int individualIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(individualIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(individualIndex, PopulationSize);

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
