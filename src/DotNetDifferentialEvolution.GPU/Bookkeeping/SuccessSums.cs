namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The sums over the improved trials that JADE's means and SHADE's memory are updated from: Σw, Σw·CR, Σw·CR², Σw·F,
/// Σw·F² and the largest CR. SHADE weighs a trial by its improvement; JADE by 1, which makes Σw the count and the
/// other sums exactly JADE's ΣCR, ΣF and ΣF² (<c>1·x</c> is <c>x</c>, <c>(1·x)·x</c> is <c>x·x</c>). Each term is
/// computed as the CPU strategies compute it, so sums added in the same order are the same bit for bit.
/// </summary>
/// <param name="Weight">Σw.</param>
/// <param name="WeightedCr">Σw·CR.</param>
/// <param name="WeightedCrSquared">Σw·CR·CR.</param>
/// <param name="WeightedF">Σw·F.</param>
/// <param name="WeightedFSquared">Σw·F·F.</param>
/// <param name="MaxCr">The largest CR, from 0.</param>
internal readonly record struct SuccessSums(
    double Weight,
    double WeightedCr,
    double WeightedCrSquared,
    double WeightedF,
    double WeightedFSquared,
    double MaxCr)
{
    /// <summary>The number of doubles one set of sums occupies in a device buffer.</summary>
    public const int Width = 6;

    /// <summary>These sums with one more trial added last.</summary>
    /// <param name="weight">The trial's weight.</param>
    /// <param name="crossoverProbability">The trial's CR.</param>
    /// <param name="mutationForce">The trial's F.</param>
    /// <returns>The sums.</returns>
    public SuccessSums Add(double weight, double crossoverProbability, double mutationForce) => new(
        Weight + weight,
        WeightedCr + weight * crossoverProbability,
        WeightedCrSquared + weight * crossoverProbability * crossoverProbability,
        WeightedF + weight * mutationForce,
        WeightedFSquared + weight * mutationForce * mutationForce,
        crossoverProbability > MaxCr ? crossoverProbability : MaxCr);

    /// <summary>These sums with a later chunk's sums added last.</summary>
    /// <param name="later">The later chunk's sums.</param>
    /// <returns>The sums.</returns>
    public SuccessSums Combine(SuccessSums later) => new(
        Weight + later.Weight,
        WeightedCr + later.WeightedCr,
        WeightedCrSquared + later.WeightedCrSquared,
        WeightedF + later.WeightedF,
        WeightedFSquared + later.WeightedFSquared,
        later.MaxCr > MaxCr ? later.MaxCr : MaxCr);
}
