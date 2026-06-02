namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// Samples from the normal and Cauchy distributions used by the adaptive DE variants
/// (JADE, SHADE, L-SHADE), built on top of a <see cref="BaseRandomProvider"/>.
/// </summary>
public static class RandomDistributionHelper
{
    /// <summary>
    /// Samples a value from a normal (Gaussian) distribution using the Box-Muller transform.
    /// </summary>
    public static double NextGaussian(
        BaseRandomProvider randomProvider,
        double mean,
        double standardDeviation)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        // Guard against log(0).
        var u1 = 1.0 - randomProvider.NextDouble();
        var u2 = 1.0 - randomProvider.NextDouble();

        var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + standardDeviation * standardNormal;
    }

    /// <summary>
    /// Samples a value from a Cauchy distribution with the given location and scale.
    /// </summary>
    public static double NextCauchy(
        BaseRandomProvider randomProvider,
        double location,
        double scale)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        var u = randomProvider.NextDouble();
        return location + scale * Math.Tan(Math.PI * (u - 0.5));
    }
}
