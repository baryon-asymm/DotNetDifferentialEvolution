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

    /// <summary>
    /// Selects individuals for the next generation and reports whether the trial replaced its
    /// parent. This is the overload the engine calls.
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
    /// external archive (it stores the parents that were actually discarded) and JADE/SHADE
    /// parameter adaptation (the control parameters credited are those of trials that actually
    /// won). A strategy whose acceptance rule is not the greedy <c>trial &lt; parent</c> — ties
    /// accepted, simulated-annealing acceptance, constraint domination — must override this, or
    /// the record will describe a different population from the one it produced.
    /// <para>
    /// The default implementation calls <see cref="Select"/> and then assumes the greedy rule, so
    /// an existing implementation keeps behaving exactly as before.
    /// </para>
    /// </remarks>
    public bool SelectTrial(
        int individualIndex,
        double trialIndividualFfValue,
        Span<double> trialIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> nextPopulationFfValues,
        Span<double> nextPopulation)
    {
        var parentFfValue = populationFfValues[individualIndex];

        Select(
            individualIndex,
            trialIndividualFfValue,
            trialIndividual,
            populationFfValues,
            population,
            nextPopulationFfValues,
            nextPopulation);

        return trialIndividualFfValue < parentFfValue;
    }
}
