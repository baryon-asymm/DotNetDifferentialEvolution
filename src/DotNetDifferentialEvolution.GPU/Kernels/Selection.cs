namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// What became of a trial, as the CPU package's <c>SelectionStrategy</c> reports it: improved (strictly better), accepted
/// (as good, when ties are accepted) or kept (the parent stays). <see cref="double.NaN"/> is worse than every real value,
/// and two <see cref="double.NaN"/>s are not a tie (<c>FitnessComparisonHelper</c>).
/// </summary>
internal static class Selection
{
    /// <summary>The parent stays.</summary>
    public const int Kept = 0;

    /// <summary>The trial replaces a parent it equals.</summary>
    public const int Accepted = 1;

    /// <summary>The trial replaces a parent it beats.</summary>
    public const int Improved = 2;

    /// <summary>The outcome of a trial against its parent.</summary>
    /// <param name="trialFitness">f(u).</param>
    /// <param name="parentFitness">f(x).</param>
    /// <param name="acceptsTies">Whether an equal trial replaces the parent.</param>
    /// <returns><see cref="Improved"/>, <see cref="Accepted"/> or <see cref="Kept"/>.</returns>
    public static int Outcome(double trialFitness, double parentFitness, bool acceptsTies) =>
        IsBetter(trialFitness, parentFitness)
            ? Improved
            : acceptsTies && IsBetterOrEqual(trialFitness, parentFitness)
                ? Accepted
                : Kept;

    /// <summary>Whether <paramref name="candidate"/> beats <paramref name="incumbent"/>: lower, or a real value against <see cref="double.NaN"/>.</summary>
    /// <param name="candidate">The candidate's fitness.</param>
    /// <param name="incumbent">The incumbent's fitness.</param>
    /// <returns>Whether the candidate is better.</returns>
    public static bool IsBetter(double candidate, double incumbent) =>
        candidate < incumbent || (double.IsNaN(incumbent) && !double.IsNaN(candidate));

    /// <summary>Whether <paramref name="candidate"/> is at least as good as <paramref name="incumbent"/>.</summary>
    /// <param name="candidate">The candidate's fitness.</param>
    /// <param name="incumbent">The incumbent's fitness.</param>
    /// <returns>Whether the candidate is better or equal.</returns>
    public static bool IsBetterOrEqual(double candidate, double incumbent) =>
        candidate <= incumbent || (double.IsNaN(incumbent) && !double.IsNaN(candidate));
}
