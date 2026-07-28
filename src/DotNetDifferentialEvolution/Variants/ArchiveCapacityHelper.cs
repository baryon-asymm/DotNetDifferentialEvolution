namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// Sizes the external archive from the rate a variant was configured with.
/// </summary>
internal static class ArchiveCapacityHelper
{
    /// <summary>
    /// Converts an archive size rate into a capacity in individuals. The papers round half up,
    /// not to even, so the midpoints are pinned explicitly.
    /// </summary>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size.</param>
    /// <param name="populationSize">The population size to scale against.</param>
    public static int Size(
        double archiveSizeRate,
        int populationSize)
        => (int)Math.Round(archiveSizeRate * populationSize, MidpointRounding.AwayFromZero);
}
