using DotNetDifferentialEvolution.Models.Interfaces;

namespace DotNetDifferentialEvolution.Models;

public class IndividualCursor : IIndividualCursor
{
    private double _fitnessFunctionValue;
    private ReadOnlyMemory<double> _genes;
    
    public double FitnessFunctionValue => _fitnessFunctionValue;
    
    public ReadOnlyMemory<double> Genes => _genes;

    public IndividualCursor(
        double fitnessFunctionValue,
        ReadOnlyMemory<double> genes)
    {
        _fitnessFunctionValue = fitnessFunctionValue;
        _genes = genes;
    }

    public void AcceptUpdater(
        int individualIndex,
        IIndividualCursorUpdater updater)
    {
        updater.Update(
            individualIndex,
            ref _fitnessFunctionValue,
            ref _genes);
    }

    public IndividualCursor GetSnapshot(bool deepCopy = false)
    {
        return new IndividualCursor(
            _fitnessFunctionValue,
            deepCopy ? _genes.ToArray() : _genes);
    }
}
