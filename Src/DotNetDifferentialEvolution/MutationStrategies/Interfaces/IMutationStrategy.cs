namespace DotNetDifferentialEvolution.MutationStrategies.Interfaces;

public interface IMutationStrategy
{
    public void Mutate(
        int individualIndex,
        Span<double> population,
        Span<double> bufferPopulation);
}