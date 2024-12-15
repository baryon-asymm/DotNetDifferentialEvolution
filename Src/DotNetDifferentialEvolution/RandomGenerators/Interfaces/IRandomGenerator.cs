namespace DotNetDifferentialEvolution.RandomGenerators.Interfaces;

public interface IRandomGenerator
{
    public double NextDouble();
    
    public double NextDouble(int individualIndex);

    public double NextInt();
    
    public double NextInt(int individualIndex);
}