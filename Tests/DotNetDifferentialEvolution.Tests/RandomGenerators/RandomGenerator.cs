using DotNetDifferentialEvolution.RandomGenerators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.RandomGenerators;

public class RandomGenerator : IRandomGenerator
{
    private readonly Random _random = Random.Shared;
    
    public double NextDouble() => _random.NextDouble();

    public double NextDouble(int individualIndex) => _random.NextDouble();

    public int NextInt() => _random.Next();

    public int NextInt(int individualIndex) => _random.Next();
}