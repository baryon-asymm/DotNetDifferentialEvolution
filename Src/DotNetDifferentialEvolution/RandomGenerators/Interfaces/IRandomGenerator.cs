namespace DotNetDifferentialEvolution.RandomGenerators.Interfaces;

public interface IRandomGenerator
{
    public double NextDouble();
    
    public double NextDouble(int individualIndex);

    public int NextInt();
    
    public int NextInt(int individualIndex);
}