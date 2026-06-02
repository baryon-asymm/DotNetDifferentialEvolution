using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.Benchmark.RandomGenerators;

public class DeterminedRandomProvider : BaseRandomProvider
{
    private readonly Random _random;
    
    public DeterminedRandomProvider(
        int Seed)
    {
        _random = new Random(Seed);
    }

    public override int Next(
        int maxValue)
    {
        return _random.Next(maxValue);
    }

    public override double NextDouble()
    {
        return _random.NextDouble();
    }
}
