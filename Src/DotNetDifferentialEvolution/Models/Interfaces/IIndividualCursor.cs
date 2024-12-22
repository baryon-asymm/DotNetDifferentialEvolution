namespace DotNetDifferentialEvolution.Models.Interfaces;

public interface IIndividualCursor
{
    public void AcceptUpdater(
        int individualIndex,
        IIndividualCursorUpdater updater);
}
