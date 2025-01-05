namespace DotNetDifferentialEvolution.Models.Interfaces;

public interface IIndividualCursorUpdater
{
    public void Update(
        int individualIndex,
        ref double fitnessFunctionValue,
        ref ReadOnlyMemory<double> genes);
}
