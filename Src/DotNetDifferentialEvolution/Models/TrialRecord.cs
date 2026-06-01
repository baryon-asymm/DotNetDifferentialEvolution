namespace DotNetDifferentialEvolution.Models;

/// <summary>
/// Records the outcome of a single trial-vector evaluation during one generation.
/// Workers fill one record per handled individual; adaptive strategies read the
/// aggregated records once per generation to adapt their control parameters.
/// </summary>
public record struct TrialRecord
{
    /// <summary>
    /// Gets or sets a value indicating whether the trial replaced its parent
    /// (i.e. the trial's fitness was strictly better).
    /// </summary>
    public bool Succeeded { get; set; }

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
