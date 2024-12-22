namespace DotNetDifferentialEvolution.RandomProviders;

public class RandomProvider : BaseRandomProvider
{
    private readonly Random _random = Random.Shared;
    
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
