using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.SelectionStrategies;

/// <summary>
/// The selection operator of the DE papers: the trial survives when it is at least as good as its
/// parent, and counts as a success only when it is strictly better.
/// </summary>
/// <remarks>
/// The two thresholds are not the same, and the difference is the point. SHADE (2013) Eq. (6) and
/// L-SHADE (2014) Algorithm 2 line 12 take the trial on <c>f(u) &lt;= f(x)</c>; line 16 records
/// the success on <c>f(u) &lt; f(x)</c>. Accepting a tie lets a population cross a plateau instead
/// of standing still on it, and reporting it as <see cref="SelectionOutcome.TrialAccepted"/>
/// rather than an improvement keeps a run of zero-gain ties out of the archive and out of the
/// parameter-adaptation memory.
/// </remarks>
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
    /// Selects the individual for the next generation based on the two fitness function values,
    /// and reports both whether the trial replaced its parent and whether it improved on it.
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
    /// Neither threshold is the plain arithmetic comparison: a parent the objective scored NaN is
    /// worse than every real value, so any real-valued trial both replaces it and is credited with
    /// the improvement, while a NaN trial replaces nothing — including a NaN parent, since
    /// swapping one unusable value for another buys nothing.
    /// </remarks>
    public SelectionOutcome Select(
        int individualIndex,
        double trialIndividualFfValue,
        Span<double> trialIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> nextPopulationFfValues,
        Span<double> nextPopulation)
    {
        var parentFfValue = populationFfValues[individualIndex];

        SelectionOutcome outcome;
        if (FitnessComparisonHelper.IsBetter(trialIndividualFfValue, parentFfValue))
            outcome = SelectionOutcome.TrialImproved;
        else if (FitnessComparisonHelper.IsBetterOrEqual(trialIndividualFfValue, parentFfValue))
            outcome = SelectionOutcome.TrialAccepted;
        else
            outcome = SelectionOutcome.ParentKept;

        if (outcome != SelectionOutcome.ParentKept)
        {
            trialIndividual.CopyTo(
                nextPopulation.Slice(individualIndex * _genomeSize, _genomeSize));

            nextPopulationFfValues[individualIndex] = trialIndividualFfValue;
        }
        else
        {
            population.Slice(individualIndex * _genomeSize, _genomeSize).CopyTo(
                nextPopulation.Slice(individualIndex * _genomeSize, _genomeSize));

            nextPopulationFfValues[individualIndex] = parentFfValue;
        }

        return outcome;
    }
}
