using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.SelectionStrategies;

public class SelectionStrategy : ISelectionStrategy
{
    private readonly int _genomeSize;

    public SelectionStrategy(
        DEContext context)
    {
        _genomeSize = context.GenomeSize;
    }

    public void Select(
        int individualIndex,
        double tempIndividualFfValue,
        Span<double> tempIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> bufferPopulationFfValues,
        Span<double> bufferPopulation)
    {
        if (tempIndividualFfValue < populationFfValues[individualIndex])
        {
            tempIndividual.CopyTo(
                bufferPopulation[(individualIndex * _genomeSize)..(individualIndex * _genomeSize + _genomeSize)]);

            bufferPopulationFfValues[individualIndex] = tempIndividualFfValue;
        }
        else
        {
            population[(individualIndex * _genomeSize)..(individualIndex * _genomeSize + _genomeSize)].CopyTo(
                bufferPopulation[(individualIndex * _genomeSize)..(individualIndex * _genomeSize + _genomeSize)]);

            bufferPopulationFfValues[individualIndex] = populationFfValues[individualIndex];
        }
    }
}
