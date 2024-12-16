namespace DotNetDifferentialEvolution.Tests.Helpers;

public class PopulationHelper
{
    private readonly int _populationSize;
    private readonly int _genomeSize;

    private readonly Memory<double> _memory;

    public PopulationHelper(int populationSize, int genomeSize)
    {
        _populationSize = populationSize;
        _genomeSize = genomeSize;

        var memorySize = 2 * _populationSize * _genomeSize + 2 * _populationSize;
        _memory = new double[memorySize];
    }

    public void InitializePopulationWithRandomValues()
    {
        for (int i = 0; i < _populationSize * _genomeSize; i++)
        {
            _memory.Span[i] = Random.Shared.NextDouble();
        }
    }

    public Memory<double> Population => _memory[..(_populationSize * _genomeSize)];

    public Memory<double> BufferPopulation =>
        _memory[(_populationSize * _genomeSize)..(2 * _populationSize * _genomeSize)];

    public Memory<double> PopulationFfValues =>
        _memory[(2 * _populationSize * _genomeSize)..(2 * _populationSize * _genomeSize + _populationSize)];

    public Memory<double> BufferPopulationFfValues => _memory[(2 * _populationSize * _genomeSize + _populationSize)..];
}