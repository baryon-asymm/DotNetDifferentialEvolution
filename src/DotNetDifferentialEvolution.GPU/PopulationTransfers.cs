using DotNetDifferentialEvolution.GPU.Bookkeeping;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The package's only host transfers (ACCEPTANCE.md, check 5a): the bounds up once, the population
/// down when the observer is due and once at the end, and under a stagnation limit the stop word:
/// read with a synchronisation before each observer call and at the end, and otherwise copied
/// every few generations to page-locked memory without one (S17, A13). It counts the downloads,
/// the synchronising reads and the copies, so a test can prove there is no per-generation round trip
/// (checks 5b, S17, A13). Host arrays cross only through the array overloads, which pin them
/// (check 8d).
/// </summary>
internal sealed class PopulationTransfers
{
    /// <summary>The value the host's copy of the stop flag holds from the moment a copy is enqueued until it has landed.</summary>
    private const int InFlight = int.MinValue;

    /// <summary>Gets the number of population downloads so far.</summary>
    public int DownloadCount { get; private set; }

    /// <summary>Gets the number of synchronising stop-word reads so far.</summary>
    public int StopReadCount { get; private set; }

    /// <summary>Gets the number of stop-word copies enqueued without a synchronisation so far.</summary>
    public int StopCopyCount { get; private set; }

    /// <summary>Copies <paramref name="source"/> into <paramref name="destination"/> on the device.</summary>
    /// <param name="destination">The device buffer, as long as the array.</param>
    /// <param name="source">The host values.</param>
    public static void Upload(MemoryBuffer1D<double, Stride1D.Dense> destination, double[] source) =>
        destination.View.CopyFromCPU(source);

    /// <summary>Copies a population and its fitness values to the host, after the device has finished its work.</summary>
    /// <param name="accelerator">The accelerator, synchronized first.</param>
    /// <param name="genes">The device genes, <c>N·D</c>.</param>
    /// <param name="fitness">The device fitness values, <c>N</c>.</param>
    /// <param name="hostGenes">Receives the genes.</param>
    /// <param name="hostFitness">Receives the fitness values.</param>
    public void Download(
        Accelerator accelerator,
        ArrayView1D<double, Stride1D.Dense> genes,
        ArrayView1D<double, Stride1D.Dense> fitness,
        double[] hostGenes,
        double[] hostFitness)
    {
        accelerator.Synchronize();
        genes.CopyToCPU(hostGenes);
        fitness.CopyToCPU(hostFitness);
        DownloadCount++;
    }

    /// <summary>Copies the stop word to the host, after the device has finished its work.</summary>
    /// <param name="accelerator">The accelerator, synchronized first.</param>
    /// <param name="stop">The device stop word.</param>
    /// <param name="hostStop">Receives the stop word.</param>
    public void ReadStop(Accelerator accelerator, ArrayView1D<int, Stride1D.Dense> stop, int[] hostStop)
    {
        accelerator.Synchronize();
        stop.CopyToCPU(hostStop);
        StopReadCount++;
    }

    /// <summary>
    /// Enqueues, on <paramref name="stream"/>, a copy of the stop word to page-locked host memory, and returns without
    /// waiting for it: the accelerator is not synchronised. The copy lands in stream order, after every kernel enqueued so
    /// far; <see cref="PollStopCopy"/> tells when it has.
    /// </summary>
    /// <param name="stream">The stream the generations are enqueued on.</param>
    /// <param name="stop">The device stop word.</param>
    /// <param name="host">The page-locked copy; no earlier copy may still be in flight.</param>
    public void BeginStopCopy(AcceleratorStream stream, ArrayView1D<int, Stride1D.Dense> stop, PageLockedArray1D<int> host)
    {
        Volatile.Write(ref host[BookkeepingKernels.StopSet], InFlight);
        stop.CopyToPageLockedAsync(stream, host);
        StopCopyCount++;
    }

    /// <summary>
    /// Looks, without waiting, at the copy begun by <see cref="BeginStopCopy"/>. The flag is the only word read: one aligned
    /// word cannot be seen half written, so a copy that has not landed is told by <see cref="InFlight"/> and never mistaken
    /// for a flag.
    /// </summary>
    /// <param name="host">The page-locked copy.</param>
    /// <returns>Whether the copy is still in flight, or the stop flag it carried.</returns>
    public static StopCopyState PollStopCopy(PageLockedArray1D<int> host)
    {
        var flag = Volatile.Read(ref host[BookkeepingKernels.StopSet]);
        return flag == InFlight ? StopCopyState.InFlight : flag == 0 ? StopCopyState.NotSet : StopCopyState.Set;
    }
}

/// <summary>What a stop-word copy has to say, once looked at without waiting.</summary>
internal enum StopCopyState
{
    /// <summary>The copy has not landed yet: nothing is known.</summary>
    InFlight = 0,

    /// <summary>The copy landed and the rule had not fired when it was taken.</summary>
    NotSet = 1,

    /// <summary>The copy landed and the rule had fired.</summary>
    Set = 2,
}
