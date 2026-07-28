namespace DotNetDifferentialEvolution.Helpers;

/// <summary>
/// The engine-wide rule for ranking two fitness values. A user-supplied objective may return
/// <see cref="double.NaN"/>, and NaN loses every IEEE comparison — both <c>NaN &lt; x</c> and
/// <c>x &lt; NaN</c> are <see langword="false"/> — so a plain <c>&lt;</c> would leave a NaN
/// individual impossible to replace and impossible to displace as the best. NaN is therefore
/// ranked as worse than every real value.
/// </summary>
internal static class FitnessComparisonHelper
{
    /// <summary>
    /// Determines whether <paramref name="candidateFfValue"/> is better (lower) than
    /// <paramref name="incumbentFfValue"/>. Equal values are not better, preserving the greedy
    /// strict-improvement rule; a real value beats NaN, and NaN never beats anything.
    /// </summary>
    /// <param name="candidateFfValue">The fitness function value of the candidate.</param>
    /// <param name="incumbentFfValue">The fitness function value of the incumbent.</param>
    /// <returns><c>true</c> if the candidate is better than the incumbent; otherwise, <c>false</c>.</returns>
    public static bool IsBetter(
        double candidateFfValue,
        double incumbentFfValue)
    {
        return candidateFfValue < incumbentFfValue
               || (double.IsNaN(incumbentFfValue) && double.IsNaN(candidateFfValue) == false);
    }

    /// <summary>
    /// Determines whether <paramref name="candidateFfValue"/> is at least as good as
    /// <paramref name="incumbentFfValue"/> — the acceptance rule of the DE papers, which take the
    /// trial on <c>f(u) &lt;= f(x)</c> so that a population can drift across a plateau instead of
    /// freezing on it.
    /// </summary>
    /// <param name="candidateFfValue">The fitness function value of the candidate.</param>
    /// <param name="incumbentFfValue">The fitness function value of the incumbent.</param>
    /// <returns><c>true</c> if the candidate is better than or equal to the incumbent.</returns>
    /// <remarks>
    /// Two NaNs are <em>not</em> equal for this purpose. Swapping one unusable value for another
    /// buys nothing, and treating it as an acceptance would make every NaN individual churn its
    /// genes every generation for no reason.
    /// </remarks>
    public static bool IsBetterOrEqual(
        double candidateFfValue,
        double incumbentFfValue)
    {
        return candidateFfValue <= incumbentFfValue
               || (double.IsNaN(incumbentFfValue) && double.IsNaN(candidateFfValue) == false);
    }
}
