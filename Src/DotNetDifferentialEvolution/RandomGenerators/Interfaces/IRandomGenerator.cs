namespace DotNetDifferentialEvolution.RandomGenerators.Interfaces;

public interface IRandomGenerator
{
    public double NextDouble();

    public int NextInt(
        int maxValue);
}
