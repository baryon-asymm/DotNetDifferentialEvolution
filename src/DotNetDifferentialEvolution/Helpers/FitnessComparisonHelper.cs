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
}
