using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.SelectionStrategies;

public class SelectionStrategy : ISelectionStrategy
{
    private readonly int _genomeSize;

    public SelectionStrategy(
        ProblemContext context)
    {
        _genomeSize = context.GenomeSize;
    }

    public void Select(
        int individualIndex,
        double trialIndividualFfValue,
        Span<double> trialIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> nextPopulationFfValues,
        Span<double> nextPopulation)
    {
        if (trialIndividualFfValue < populationFfValues[individualIndex])
        {
            trialIndividual.CopyTo(
                nextPopulation.Slice(individualIndex * _genomeSize, _genomeSize));

            nextPopulationFfValues[individualIndex] = trialIndividualFfValue;
        }
        else
        {
            population.Slice(individualIndex * _genomeSize, _genomeSize).CopyTo(
                nextPopulation.Slice(individualIndex * _genomeSize, _genomeSize));

            nextPopulationFfValues[individualIndex] = populationFfValues[individualIndex];
        }
    }
}
