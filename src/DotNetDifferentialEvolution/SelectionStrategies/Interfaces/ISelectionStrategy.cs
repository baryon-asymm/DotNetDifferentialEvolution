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
    /// <returns>Which of the two was written, and whether it was an improvement.</returns>
    /// <remarks>
    /// <para>
    /// The return value becomes <see cref="Models.TrialRecord.Outcome"/>, and the two things it
    /// distinguishes are consumed separately. Whether the trial <em>survived</em> decides what the
    /// population holds and, under jDE, which control parameters the individual carries forward.
    /// Whether it <em>improved</em> drives the external archive — which stores the parents of
    /// improving trials — and JADE/SHADE/L-SHADE parameter adaptation. Reporting an outcome other
    /// than the one written here desynchronizes both from the population.
    /// </para>
    /// <para>
    /// A rule that admits ties reports <see cref="SelectionOutcome.TrialAccepted"/> for them: the
    /// trial is in the population, but a zero-improvement replacement is not a success and must
    /// not be credited as one.
    /// </para>
    /// </remarks>
    public SelectionOutcome Select(
        int individualIndex,
        double trialIndividualFfValue,
        Span<double> trialIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> nextPopulationFfValues,
        Span<double> nextPopulation);
}
