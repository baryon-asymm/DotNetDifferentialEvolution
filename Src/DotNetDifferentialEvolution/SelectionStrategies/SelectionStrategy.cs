using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.SelectionStrategies;

/// <summary>
/// Represents a selection strategy for Differential Evolution.
/// </summary>
public class SelectionStrategy : ISelectionStrategy
{
    private readonly int _genomeSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionStrategy"/> class.
    /// </summary>
    /// <param name="genomeSize">The size of the genome.</param>
    public SelectionStrategy(
        int genomeSize)
    {
        _genomeSize = genomeSize;
    }

    /// <summary>
    /// Selects individuals for the next generation based on their fitness function values.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to select.</param>
    /// <param name="trialIndividualFfValue">The fitness function value of the trial individual.</param>
    /// <param name="trialIndividual">The trial individual to be selected.</param>
    /// <param name="populationFfValues">The fitness function values of the current population.</param>
    /// <param name="population">The current population of individuals.</param>
    /// <param name="nextPopulationFfValues">The fitness function values of the next population.</param>
    /// <param name="nextPopulation">The next population of individuals.</param>
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
