using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// Calls <see cref="DeStep.BuildTrial"/> on the host, over buffers of ILGPU's CPU accelerator: their
/// views are host memory, so the kernel's own function runs unchanged with a scripted draw source.
/// One instance holds one context and accelerator; <see cref="BuildTrial"/> allocates per call.
/// </summary>
internal sealed class HostStep : IDisposable
{
    private readonly Context _context = Context.Create(builder => builder.CPU());
    private readonly CPUAccelerator _accelerator;

    /// <summary>Initializes the CPU context and accelerator.</summary>
    public HostStep()
    {
        _accelerator = _context.CreateCPUAccelerator(0);
    }

    /// <summary>Gets the CPU accelerator, for callers that allocate their own buffers.</summary>
    public Accelerator Accelerator => _accelerator;

    /// <summary>Builds the trial of <paramref name="individual"/> and returns its D genes.</summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The draws; consumed in place.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="mutationForce">F.</param>
    /// <param name="crossoverProbability">CR, scaled by <see cref="DeStep.CrossoverThreshold"/>.</param>
    /// <param name="population">The population, individual-major, <c>N·D</c> genes.</param>
    /// <param name="lowerBound">The lower bounds, D.</param>
    /// <param name="upperBound">The upper bounds, D.</param>
    /// <returns>Slot i of the trial buffer.</returns>
    public double[] BuildTrial<TDraws>(
        ref TDraws draws,
        int individual,
        double mutationForce,
        double crossoverProbability,
        double[] population,
        double[] lowerBound,
        double[] upperBound)
        where TDraws : struct, IDrawSource
    {
        var genomeSize = lowerBound.Length;
        var parameters = new StepParameters(
            Seed: 0,
            Generation: 1,
            PopulationSize: population.Length / genomeSize,
            GenomeSize: genomeSize,
            MutationForce: mutationForce,
            CrossoverThreshold: DeStep.CrossoverThreshold(crossoverProbability));
        using var populationBuffer = Upload(population);
        using var trialBuffer = _accelerator.Allocate1D<double>(population.Length);
        using var lowerBuffer = Upload(lowerBound);
        using var upperBuffer = Upload(upperBound);
        trialBuffer.View.CopyFromCPU(new double[population.Length]);

        DeStep.BuildTrial(ref draws, individual, parameters, populationBuffer.View, trialBuffer.View, lowerBuffer.View, upperBuffer.View);

        var trial = new double[population.Length];
        trialBuffer.View.CopyToCPU(trial);
        return trial[(individual * genomeSize)..((individual + 1) * genomeSize)];
    }

    /// <summary>
    /// Builds the trial of <paramref name="individual"/> by <see cref="Schemes.BuildTrial"/> under
    /// <paramref name="parameters"/>' scheme and returns its D genes (ACCEPTANCE.md, S2, S3).
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The draws; consumed in place.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="parameters">The scheme, N, D and the p-best range.</param>
    /// <param name="mutationForce">F.</param>
    /// <param name="crossoverProbability">CR, scaled by <see cref="DeStep.CrossoverThreshold"/>.</param>
    /// <param name="population">The population, individual-major, <c>N·D</c> genes.</param>
    /// <param name="lowerBound">The lower bounds, D.</param>
    /// <param name="upperBound">The upper bounds, D.</param>
    /// <param name="state">The best index, the ranking and the archive the scheme reads.</param>
    /// <returns>Slot i of the trial buffer.</returns>
    public double[] BuildSchemeTrial<TDraws>(
        ref TDraws draws,
        int individual,
        StepParameters parameters,
        double mutationForce,
        double crossoverProbability,
        double[] population,
        double[] lowerBound,
        double[] upperBound,
        SchemeState state)
        where TDraws : struct, IDrawSource
    {
        ArgumentNullException.ThrowIfNull(state);
        var genomeSize = lowerBound.Length;
        using var populationBuffer = Upload(population);
        using var trialBuffer = _accelerator.Allocate1D<double>(population.Length);
        using var lowerBuffer = Upload(lowerBound);
        using var upperBuffer = Upload(upperBound);
        using var stop = UploadInts([0]);
        using var best = UploadInts([state.BestIndex]);
        using var ranking = UploadInts(state.Ranking.Length == 0 ? [0] : state.Ranking);
        using var archive = Upload(state.Archive.Length == 0 ? [0.0] : state.Archive);
        using var archiveSize = UploadInts([state.ArchiveSize, 0]);
        using var unused = Upload([0.0]);
        using var unusedInts = UploadInts([0]);
        trialBuffer.View.CopyFromCPU(new double[population.Length]);
        var views = new PopulationViews(populationBuffer.View, unused.View, unused.View, unused.View, trialBuffer.View, lowerBuffer.View, upperBuffer.View);
        var strategy = new StrategyViews(stop.View, best.View, ranking.View, archive.View, archiveSize.View, unused.View, unused.View, unused.View, unusedInts.View);

        Schemes.BuildTrial(ref draws, individual, parameters, mutationForce, DeStep.CrossoverThreshold(crossoverProbability), views, strategy);

        var trial = new double[population.Length];
        trialBuffer.View.CopyToCPU(trial);
        return trial[(individual * genomeSize)..((individual + 1) * genomeSize)];
    }

    /// <summary>A buffer holding <paramref name="values"/>.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The buffer; the caller disposes it.</returns>
    public MemoryBuffer1D<int, Stride1D.Dense> UploadInts(int[] values)
    {
        var buffer = _accelerator.Allocate1D<int>(values.Length);
        buffer.View.CopyFromCPU(values);
        return buffer;
    }

    /// <summary>A buffer holding <paramref name="values"/>.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The buffer; the caller disposes it.</returns>
    public MemoryBuffer1D<double, Stride1D.Dense> Upload(double[] values)
    {
        var buffer = _accelerator.Allocate1D<double>(values.Length);
        buffer.View.CopyFromCPU(values);
        return buffer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _accelerator.Dispose();
        _context.Dispose();
    }
}
