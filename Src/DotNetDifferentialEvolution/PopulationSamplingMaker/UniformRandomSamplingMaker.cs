using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.PopulationSamplingMaker;

public class UniformRandomSamplingMaker : IPopulationSamplingMaker
{
    private readonly ReadOnlyMemory<double> _lowerBound;
    private readonly ReadOnlyMemory<double> _upperBound;
    
    public UniformRandomSamplingMaker(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound)
    {
        _lowerBound = lowerBound;
        _upperBound = upperBound;
    }
    
    public void SamplePopulation(
        Span<double> population)
    {
        var genomeSize = _lowerBound.Length;
        
        var random = Random.Shared;
        for (int i = 0; i < population.Length; i++)
        {
            var geneIndex = i % genomeSize;
            population[i] = random.NextDouble() * (_upperBound.Span[geneIndex] - _lowerBound.Span[geneIndex])
                            + _lowerBound.Span[geneIndex];
        }
    }
}
