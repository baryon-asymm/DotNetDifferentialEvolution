using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.Benchmark.RandomGenerators;

public class DeterminedRandomProvider : RandomProvider
{
    public DeterminedRandomProvider(
        int Seed) : base(Seed)
    {
    }
}
