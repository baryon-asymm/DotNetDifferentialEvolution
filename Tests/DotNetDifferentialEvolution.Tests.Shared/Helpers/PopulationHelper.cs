using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.Helpers;

public class PopulationHelper
{
    private readonly int _genomeSize;

    private readonly Memory<double> _memory;
    private readonly int _populationSize;

    public PopulationHelper(
        int populationSize,
        int genomeSize)
    {
        _populationSize = populationSize;
        _genomeSize = genomeSize;

        var memorySize = 2 * _populationSize * _genomeSize + 2 * _populationSize;
        _memory = new double[memorySize];
    }

    public Memory<double> Population => _memory[..(_populationSize * _genomeSize)];

    public Memory<double> BufferPopulation =>
        _memory[(_populationSize * _genomeSize)..(2 * _populationSize * _genomeSize)];

    public Memory<double> PopulationFfValues =>
        _memory[(2 * _populationSize * _genomeSize)..(2 * _populationSize * _genomeSize + _populationSize)];

    public Memory<double> BufferPopulationFfValues => _memory[(2 * _populationSize * _genomeSize + _populationSize)..];

    public void InitializePopulationWithRandomValues()
    {
        for (var i = 0; i < _populationSize * _genomeSize; i++)
            _memory.Span[i] = Random.Shared.NextDouble();
    }
    
    public void EvaluatePopulationFfValues(IFitnessFunctionEvaluator evaluator)
    {
        var populationFfValues = PopulationFfValues.Span;
        var population = Population.Span;
        
        for (var i = 0; i < _populationSize; i++)
            populationFfValues[i] = evaluator.Evaluate(population[(i * _genomeSize)..(i * _genomeSize + _genomeSize)]);
    }
}
