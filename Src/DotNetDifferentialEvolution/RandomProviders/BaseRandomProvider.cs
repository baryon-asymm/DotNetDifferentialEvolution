namespace DotNetDifferentialEvolution.RandomProviders;

public abstract class BaseRandomProvider
{
    public abstract int Next(int maxValue);
    
    public abstract double NextDouble();
}
