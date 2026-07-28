using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.Models;

/// <summary>
/// Records the outcome of a single trial-vector evaluation during one generation.
/// Workers fill one record per handled individual; adaptive strategies read the
/// aggregated records once per generation to adapt their control parameters.
/// </summary>
public record struct TrialRecord
{
    /// <summary>
    /// Gets or sets what the selection strategy did with this trial. Defaults to
    /// <see cref="SelectionOutcome.ParentKept"/>, so an untouched record reads as "nothing
    /// happened" — which is what an individual outside the active population is.
    /// </summary>
    public SelectionOutcome Outcome { get; set; }

    /// <summary>
    /// Gets a value indicating whether the trial is in the next generation, whether or not it
    /// improved on its parent. This is what jDE keys parameter inheritance on: the individual
    /// carried forward is the trial, so the parameters carried forward are the trial's.
    /// </summary>
    public readonly bool Replaced => Outcome != SelectionOutcome.ParentKept;

    /// <summary>
    /// Gets a value indicating whether the trial was strictly better than its parent. This is what
    /// the external archive and JADE/SHADE/L-SHADE parameter adaptation are keyed on — a trial
    /// accepted on a tie changes the population but has taught the search nothing.
    /// </summary>
    public readonly bool Improved => Outcome == SelectionOutcome.TrialImproved;

    /// <summary>
    /// Gets or sets the mutation factor (F) used to generate the trial.
    /// </summary>
    public double UsedF { get; set; }

    /// <summary>
    /// Gets or sets the crossover probability (CR) used to generate the trial.
    /// </summary>
    public double UsedCr { get; set; }

    /// <summary>
    /// Gets or sets the fitness function value of the parent individual.
    /// </summary>
    public double ParentFfValue { get; set; }

    /// <summary>
    /// Gets or sets the fitness function value of the trial individual.
    /// </summary>
    public double TrialFfValue { get; set; }
}
