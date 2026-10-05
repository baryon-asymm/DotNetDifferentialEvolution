using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The package's only host transfers (ACCEPTANCE.md, check 5a): the bounds up once, the population
/// down when the observer is due and once at the end, and under a stagnation limit the stop word
/// every few generations (S17). It counts the downloads and the reads, so a test can prove there is
/// no per-generation round trip (checks 5b, S17). Host arrays cross only through the array
/// overloads, which pin them (check 8d).
/// </summary>
internal sealed class PopulationTransfers
{
    /// <summary>Gets the number of population downloads so far.</summary>
    public int DownloadCount { get; private set; }

    /// <summary>Gets the number of stop-word reads so far.</summary>
    public int StopReadCount { get; private set; }

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
}
