using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// JADE (Zhang &amp; Sanderson, 2009): <c>DE/current-to-pbest/1</c> with an optional external
/// archive of discarded parents, F sampled from a Cauchy distribution around an adaptive
/// <c>μF</c> and CR from a normal distribution around an adaptive <c>μCR</c>.
/// </summary>
public sealed class JadeVariant : IDeVariant
{
    private readonly double _pBestRate;
    private readonly double _archiveSizeRate;
    private readonly double _adaptationRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="JadeVariant"/> class.
    /// </summary>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size (0 disables the archive).</param>
    /// <param name="adaptationRate">The adaptation rate (c) for the parameter means.</param>
    public JadeVariant(
        double pBestRate = 0.1,
        double archiveSizeRate = 1.0,
        double adaptationRate = JadeStrategy.DefaultAdaptationRate)
    {
        if (archiveSizeRate < 0.0)
            throw new ArgumentOutOfRangeException(nameof(archiveSizeRate), "Archive size rate must be non-negative.");

        _pBestRate = pBestRate;
        _archiveSizeRate = archiveSizeRate;
        _adaptationRate = adaptationRate;
    }

    /// <inheritdoc />
    public DeVariantSetup Configure(
        in DeVariantConfiguration configuration)
    {
        var jadeStrategy = new JadeStrategy(
            populationSize: configuration.PopulationSize,
            adaptationRate: _adaptationRate);

        return new DeVariantSetup
        {
            MutationStrategy = new CurrentToPBestMutationStrategy(_pBestRate),
            ControlParameterProvider = jadeStrategy,
            GenerationStrategy = jadeStrategy,
            // JADE Table I lines 20-21 keep the parent when f(x) <= f(u), so a tie does not
            // displace it — the opposite of SHADE/L-SHADE. JADE does not need their split, because
            // its parameter means are unweighted and a tie therefore cannot enter one with weight
            // zero; the strict rule is simply what the paper specifies.
            SelectionStrategy = new SelectionStrategy(configuration.GenomeSize, acceptsTies: false),
            ArchiveCapacity = ArchiveCapacityHelper.Size(_archiveSizeRate, configuration.PopulationSize)
        };
    }
}
