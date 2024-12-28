using DotNetDifferentialEvolution.Models.Interfaces;

namespace DotNetDifferentialEvolution.Models;

public class Population : IIndividualCursorUpdater
{
    private readonly ReadOnlyMemory<double> _genes;
    private readonly ReadOnlyMemory<double> _fitnessFunctionValues;
    
    public IndividualCursor IndividualCursor { get; init; }
    
    public int Generation { get; set; }

    public int GenomeSize => _genes.Length / PopulationSize;

    public int PopulationSize => _fitnessFunctionValues.Length;

    public int BestIndividualIndex { get; set; }
    
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
    
    public void MoveCursorTo(
        int individualIndex)
    {
        IndividualCursor.AcceptUpdater(
            individualIndex,
            this);
    }
    
    public void MoveCursorToBestIndividual()
    {
        MoveCursorTo(BestIndividualIndex);
    }

    public void Update(
        int individualIndex,
        ref double fitnessFunctionValue,
        ref ReadOnlyMemory<double> genes)
    {
        fitnessFunctionValue = _fitnessFunctionValues.Span[individualIndex];
        genes = _genes.Slice(individualIndex * GenomeSize, GenomeSize);
    }
}
