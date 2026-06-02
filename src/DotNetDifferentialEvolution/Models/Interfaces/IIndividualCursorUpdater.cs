namespace DotNetDifferentialEvolution.Models.Interfaces;

/// <summary>
/// Interface for updating an individual cursor.
/// </summary>
public interface IIndividualCursorUpdater
{
    /// <summary>
    /// Updates the individual at the specified index.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to be updated.</param>
    /// <param name="fitnessFunctionValue">The fitness function value of the individual.</param>
    /// <param name="genes">The genes of the individual.</param>
    public void Update(
        int individualIndex,
        ref double fitnessFunctionValue,
        ref ReadOnlyMemory<double> genes);
}
