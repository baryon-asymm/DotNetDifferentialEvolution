namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// Represents the base class for random number providers.
/// </summary>
public abstract class BaseRandomProvider
{
    /// <summary>
    /// Returns a non-negative random integer that is less than the specified maximum.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound of the random number to be generated.</param>
    /// <returns>A non-negative random integer that is less than <paramref name="maxValue"/>.</returns>
    public abstract int Next(int maxValue);
    
    /// <summary>
    /// Returns a random floating-point number that is greater than or equal to 0.0 and less than 1.0.
    /// </summary>
    /// <returns>A double-precision floating-point number that is greater than or equal to 0.0 and less than 1.0.</returns>
    public abstract double NextDouble();
}
