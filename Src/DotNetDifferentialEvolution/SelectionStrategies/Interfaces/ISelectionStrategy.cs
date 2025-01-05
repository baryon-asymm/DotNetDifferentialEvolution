namespace DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

/// <summary>
/// Defines the interface for selection strategies in Differential Evolution.
/// </summary>
public interface ISelectionStrategy
{
    /// <summary>
    /// Selects individuals for the next generation.
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
        Span<double> nextPopulation);
}
