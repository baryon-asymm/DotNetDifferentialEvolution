namespace DotNetDifferentialEvolution.SelectionStrategies;

/// <summary>
/// What a selection strategy did with one trial vector.
/// </summary>
/// <remarks>
/// <para>
/// Two questions, not one. <em>Did the trial survive into the next generation?</em> decides what
/// the population now holds. <em>Was it an improvement?</em> decides what the adaptive machinery
/// learns from it — the external archive stores the parent of an improving trial, and JADE, SHADE
/// and L-SHADE credit only an improving trial's F and CR.
/// </para>
/// <para>
/// The DE literature separates them deliberately: SHADE (2013) Eq. (6) and L-SHADE (2014)
/// Algorithm 2 line 12 accept a trial on <c>f(u) &lt;= f(x)</c>, so a tie survives, while line 16
/// records the success on the strict <c>f(u) &lt; f(x)</c>. Accepting ties is what lets a
/// population drift sideways across a plateau instead of freezing on it; keeping the success
/// record strict is what stops a run of zero-improvement ties from dragging the parameter
/// adaptation with them.
/// </para>
/// <para>
/// An enumeration rather than two booleans, so that the invariant holds by construction: a trial
/// cannot improve on its parent without also replacing it. <see cref="ParentKept"/> is the default
/// value, so a zeroed <see cref="Models.TrialRecord"/> reads as "nothing happened".
/// </para>
/// </remarks>
public enum SelectionOutcome
{
    /// <summary>The parent survived; the trial was discarded.</summary>
    ParentKept = 0,

    /// <summary>
    /// The trial replaced its parent without being strictly better — a tie under an acceptance
    /// rule that admits them. It is in the population, but it is not a success: it contributes
    /// neither a parent to the archive nor its parameters to the adaptation memory.
    /// </summary>
    TrialAccepted = 1,

    /// <summary>
    /// The trial replaced its parent and was strictly better. This is the outcome the archive and
    /// the parameter adaptation are keyed on.
    /// </summary>
    TrialImproved = 2
}
