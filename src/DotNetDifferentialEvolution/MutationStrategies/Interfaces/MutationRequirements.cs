namespace DotNetDifferentialEvolution.MutationStrategies.Interfaces;

/// <summary>
/// What a mutation strategy needs the engine to provision for it, declared as data on
/// <see cref="IMutationStrategy.Requirements"/> so the engine can supply it — or refuse to
/// build — instead of handing over an unusable <see cref="MutationContext"/> and letting the
/// run silently produce nonsense.
/// </summary>
[Flags]
public enum MutationRequirements
{
    /// <summary>
    /// The strategy is self-contained: it uses only the population, the bounds and the random
    /// provider, and carries any control parameters of its own.
    /// </summary>
    None = 0,

    /// <summary>
    /// The strategy reads <see cref="MutationContext.MutationForce"/> and
    /// <see cref="MutationContext.CrossoverProbability"/>, so an
    /// <see cref="ControlParameterProviders.IControlParameterProvider"/> must be configured.
    /// Without one both values are <see cref="double.NaN"/>, which propagates through the mutant
    /// vector and makes every trial lose selection — a run that completes normally and returns
    /// the best of the initial random sample. The builder rejects the combination.
    /// </summary>
    ControlParameters = 1 << 0,

    /// <summary>
    /// The strategy reads <see cref="MutationContext.FitnessSortedIndices"/> (p-best selection).
    /// The engine re-ranks the active population at the end of every generation when this is
    /// declared, so the ranking is never the stale one from an earlier generation.
    /// </summary>
    FitnessRanking = 1 << 1,

    /// <summary>
    /// The strategy draws from <see cref="MutationContext.Archive"/> when one is configured.
    /// This is a capability, not a precondition: an empty archive is valid and the strategy is
    /// expected to fall back to the population alone.
    /// </summary>
    Archive = 1 << 2,

    /// <summary>
    /// The strategy reads <see cref="MutationContext.BestIndividualIndex"/>. The engine always
    /// supplies it; declaring it documents the dependency and lets a future engine skip work no
    /// strategy asked for.
    /// </summary>
    BestIndividual = 1 << 3
}
