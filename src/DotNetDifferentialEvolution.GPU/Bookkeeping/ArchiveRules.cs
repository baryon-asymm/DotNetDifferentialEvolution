using DotNetDifferentialEvolution.GPU.Random;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// Where an improved parent goes in the archive, as the CPU package's <c>AdaptiveStrategyBase.UpdateArchive</c> puts it
/// (ACCEPTANCE.md, S8). The CPU loop walks the improved parents in index order: below the capacity a parent takes the
/// next free slot, at the capacity a uniform slot. Here a parent's fill position is the archive's size before the
/// generation plus the number of improved parents before it, which is the size the loop has reached when it gets there;
/// and of two parents on one slot the later one stays, as the loop overwrites.
/// </summary>
internal static class ArchiveRules
{
    /// <summary>The slot of an improved parent.</summary>
    /// <typeparam name="TDraws">The draw source: the parent's own archive stream.</typeparam>
    /// <param name="draws">The parent's draws; one index is drawn only at or above the capacity.</param>
    /// <param name="fillPosition">The archive's size when the loop reaches this parent, were it unbounded.</param>
    /// <param name="capacity">The capacity; at least 1.</param>
    /// <returns>The slot.</returns>
    public static int SlotOf<TDraws>(ref TDraws draws, int fillPosition, int capacity)
        where TDraws : struct, IDrawSource =>
        fillPosition < capacity ? fillPosition : draws.NextIndex(capacity);

    /// <summary>The archive's capacity for a population: <c>round(rate·N)</c>, half away from zero (<c>ArchiveCapacityHelper</c>, <c>LShadeStrategy</c>).</summary>
    /// <param name="archiveSizeRate">The rate.</param>
    /// <param name="populationSize">N.</param>
    /// <returns>The capacity.</returns>
    public static int Capacity(double archiveSizeRate, int populationSize) =>
        (int)Math.Round(archiveSizeRate * populationSize, MidpointRounding.AwayFromZero);
}
