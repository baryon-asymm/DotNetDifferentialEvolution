using DotNetDifferentialEvolution.RandomGenerators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.RandomGenerators;

public class RandomGenerator : IRandomGenerator
{
    private readonly Random _random = Random.Shared;

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
