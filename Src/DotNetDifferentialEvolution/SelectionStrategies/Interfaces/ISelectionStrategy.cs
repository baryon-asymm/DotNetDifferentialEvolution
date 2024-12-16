namespace DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

public interface ISelectionStrategy
{
    public void Select(
        int individualIndex,
        Span<double> population,
        Span<double> bufferPopulation);
}