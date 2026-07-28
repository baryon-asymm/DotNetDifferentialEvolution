namespace DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

/// <summary>
/// Decides, for one individual, whether its trial replaces it — and writes the winner into the
/// next generation.
/// </summary>
public interface ISelectionStrategy
{
    /// <summary>
    /// Writes either the trial or its parent into the next generation at
    /// <paramref name="individualIndex"/>, and reports which one it was.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to select.</param>
    /// <param name="trialIndividualFfValue">The fitness function value of the trial individual.</param>
    /// <param name="trialIndividual">The trial individual to be selected.</param>
    /// <param name="populationFfValues">The fitness function values of the current population.</param>
    /// <param name="population">The current population of individuals.</param>
    /// <param name="nextPopulationFfValues">The fitness function values of the next population.</param>
    /// <param name="nextPopulation">The next population of individuals.</param>
    /// <returns><see langword="true"/> when the trial replaced its parent.</returns>
    /// <remarks>
    /// The return value becomes <see cref="Models.TrialRecord.Succeeded"/>, which drives the
    /// external archive — it stores the parents that were actually discarded — and JADE/SHADE
    /// parameter adaptation, which credits the control parameters of trials that actually won.
    /// Reporting an outcome other than the one written here desynchronizes both from the
    /// population.
    /// </remarks>
    public bool Select(
        int individualIndex,
        double trialIndividualFfValue,
        Span<double> trialIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> nextPopulationFfValues,
        Span<double> nextPopulation);
}
