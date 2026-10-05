using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// The archive's update on the host, by the package's rule as BOOT.md states it (Bookkeeping/ACCEPTANCE.md, S8): the size before
/// cut to the capacity; each improved parent, in index order, at its fill position or a drawn slot
/// (<see cref="ArchiveRules.SlotOf"/>); on a shared slot the highest index; then the copies.
/// </summary>
internal static class HostArchive
{
    /// <summary>The draws of parent i, returned by reference so they are consumed in place.</summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="individual">The parent's index.</param>
    /// <returns>The parent's draws.</returns>
    internal delegate ref TDraws DrawsOf<TDraws>(int individual)
        where TDraws : struct, GPU.Random.IDrawSource;

    /// <summary>Updates <paramref name="archive"/> in place and returns the new size.</summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="archive">The archive, capacity·D.</param>
    /// <param name="sizeBefore">The size before the generation.</param>
    /// <param name="capacity">The capacity; 0 leaves everything as it is.</param>
    /// <param name="genomeSize">D.</param>
    /// <param name="parents">The discarded parents, N·D.</param>
    /// <param name="outcomes">The trials' outcomes.</param>
    /// <param name="drawsOf">Each parent's draws.</param>
    /// <returns>The new size.</returns>
    public static int Update<TDraws>(
        double[] archive, int sizeBefore, int capacity, int genomeSize, double[] parents, int[] outcomes, DrawsOf<TDraws> drawsOf)
        where TDraws : struct, GPU.Random.IDrawSource
    {
        if (capacity <= 0)
        {
            return sizeBefore;
        }

        var size = Math.Min(sizeBefore, capacity);
        var owners = Enumerable.Repeat(-1, capacity).ToArray();
        var fillPosition = size;
        for (var i = 0; i < outcomes.Length; i++)
        {
            if (outcomes[i] != Selection.Improved)
            {
                continue;
            }

            var slot = ArchiveRules.SlotOf(ref drawsOf(i), fillPosition, capacity);
            owners[slot] = Math.Max(owners[slot], i);
            fillPosition++;
        }

        for (var slot = 0; slot < capacity; slot++)
        {
            if (owners[slot] >= 0)
            {
                Array.Copy(parents, owners[slot] * genomeSize, archive, slot * genomeSize, genomeSize);
            }
        }

        return Math.Min(capacity, fillPosition);
    }

    /// <summary>Which of the four kinds of case S8 names this one is: fills, fills into overflow, starts full, has no capacity.</summary>
    /// <param name="sizeBefore">The size before.</param>
    /// <param name="capacity">The capacity.</param>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>0, 1, 2 or 3.</returns>
    public static int Kind(int sizeBefore, int capacity, int[] outcomes)
    {
        var improved = outcomes.Count(outcome => outcome == Selection.Improved);
        return capacity == 0 ? 3 : sizeBefore >= capacity ? 2 : sizeBefore + improved > capacity ? 1 : 0;
    }
}
