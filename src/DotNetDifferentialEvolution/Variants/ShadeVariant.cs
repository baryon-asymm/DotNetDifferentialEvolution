using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// SHADE (Tanabe &amp; Fukunaga, 2013): JADE's <c>DE/current-to-pbest/1</c> with archive, with the
/// single adaptive mean replaced by a memory of <c>H</c> successful <c>(M_F, M_CR)</c> pairs, and
/// the p-best rate sampled per individual from <c>[2/N, pBestRate]</c>.
/// </summary>
public sealed class ShadeVariant : IDeVariant
{
    private readonly double _pBestRate;
    private readonly double _archiveSizeRate;
    private readonly int _memorySize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShadeVariant"/> class.
    /// </summary>
    /// <param name="pBestRate">The upper bound (0, 1] of the per-individual p-best pool fraction
    /// (the SHADE paper uses 0.2).</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size (0 disables the archive).</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    public ShadeVariant(
        double pBestRate = 0.2,
        double archiveSizeRate = 1.0,
        int memorySize = ShadeStrategy.DefaultMemorySize)
    {
        if (archiveSizeRate < 0.0)
            throw new ArgumentOutOfRangeException(nameof(archiveSizeRate), "Archive size rate must be non-negative.");

        _pBestRate = pBestRate;
        _archiveSizeRate = archiveSizeRate;
        _memorySize = memorySize;
    }

    /// <inheritdoc />
    public DeVariantSetup Configure(
        in DeVariantConfiguration configuration)
    {
        var shadeStrategy = new ShadeStrategy(
            populationSize: configuration.PopulationSize,
            memorySize: _memorySize);

        // SHADE samples p per individual from [2/N, pBestRate]; cap the lower bound at pBestRate
        // so very small populations degenerate to a fixed rate instead of an invalid range.
        var pBestRateMin = Math.Min(2.0 / configuration.PopulationSize, _pBestRate);

        return new DeVariantSetup
        {
            MutationStrategy = new CurrentToPBestMutationStrategy(pBestRateMin, _pBestRate),
            ControlParameterProvider = shadeStrategy,
            GenerationStrategy = shadeStrategy,
            SelectionStrategy = new SelectionStrategy(configuration.GenomeSize),
            ArchiveCapacity = ArchiveCapacityHelper.Size(_archiveSizeRate, configuration.PopulationSize)
        };
    }
}
