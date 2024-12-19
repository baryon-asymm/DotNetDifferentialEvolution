using DotNetDifferentialEvolution.RandomGenerators.Interfaces;

namespace DotNetDifferentialEvolution.Benchmark.RandomGenerators;

public class DeterminedRandomGenerator : IRandomGenerator
{
    private readonly Random _random;
    
    public DeterminedRandomGenerator(
        int seed)
    {
        _random = new Random(seed);
    }

    public double NextDouble()
    {
        return _random.NextDouble();
    }

    public int NextInt(
        int maxValue)
    {
        return _random.Next(maxValue);
    }
}
