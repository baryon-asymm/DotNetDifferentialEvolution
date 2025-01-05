namespace DotNetDifferentialEvolution.Models.Interfaces;

/// <summary>
/// Interface for an individual cursor.
/// </summary>
public interface IIndividualCursor
{
    /// <summary>
    /// Accepts an updater for the individual at the specified index.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to be updated.</param>
    /// <param name="updater">The updater to be applied to the individual.</param>
    public void AcceptUpdater(
        int individualIndex,
        IIndividualCursorUpdater updater);
}
