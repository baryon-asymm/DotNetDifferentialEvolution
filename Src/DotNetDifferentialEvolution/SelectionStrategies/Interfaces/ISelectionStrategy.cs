namespace DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

public interface ISelectionStrategy
{
    public void Select(
        int individualIndex,
        double tempIndividualFfValue,
        Span<double> tempIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> bufferPopulationFfValues,
        Span<double> bufferPopulation);
}
