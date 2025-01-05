using DotNetDifferentialEvolution.Models.Interfaces;

namespace DotNetDifferentialEvolution.Models;

/// <summary>
/// Represents an individual cursor with fitness function value and genes.
/// </summary>
public class IndividualCursor : IIndividualCursor
{
    private double _fitnessFunctionValue;
    private ReadOnlyMemory<double> _genes;
    
    /// <summary>
    /// Gets the fitness function value of the individual.
    /// </summary>
    public double FitnessFunctionValue => _fitnessFunctionValue;
    
    /// <summary>
    /// Gets the genes of the individual.
    /// </summary>
    public ReadOnlyMemory<double> Genes => _genes;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndividualCursor"/> class.
    /// </summary>
    /// <param name="fitnessFunctionValue">The fitness function value of the individual.</param>
    /// <param name="genes">The genes of the individual.</param>
    public IndividualCursor(
        double fitnessFunctionValue,
        ReadOnlyMemory<double> genes)
    {
        _fitnessFunctionValue = fitnessFunctionValue;
        _genes = genes;
    }

    /// <summary>
    /// Accepts an updater for the individual at the specified index.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to be updated.</param>
    /// <param name="updater">The updater to be applied to the individual.</param>
    public void AcceptUpdater(
        int individualIndex,
        IIndividualCursorUpdater updater)
    {
        updater.Update(
            individualIndex,
            ref _fitnessFunctionValue,
            ref _genes);
    }

    /// <summary>
    /// Gets a snapshot of the individual cursor.
    /// </summary>
    /// <param name="deepCopy">Indicates whether to perform a deep copy of the genes.</param>
    /// <returns>A snapshot of the individual cursor.</returns>
    public IndividualCursor GetSnapshot(bool deepCopy = false)
    {
        return new IndividualCursor(
            _fitnessFunctionValue,
            deepCopy ? _genes.ToArray() : _genes);
    }
}
