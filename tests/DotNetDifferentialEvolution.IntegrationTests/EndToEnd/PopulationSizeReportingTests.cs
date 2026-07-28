using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// L-SHADE shrinks the population as the evaluation budget is consumed, but the buffers stay at
/// their initial length — nothing is reallocated. <see cref="Population.PopulationSize"/> used to
/// report that buffer length, so an observer iterating it read individuals that had been dropped
/// generations earlier: measured at 50 reported against 4 live, with the stale entries spanning
/// `6.72E-15` to `7.28E+01` while the live ones sat in `[1.70E-15, 3.35E-15]`.
/// <see cref="Population.PopulationSize"/> is now the active size and
/// <see cref="Population.Capacity"/> is the allocated one.
/// </summary>
[Trait("Category", "Integration")]
public class PopulationSizeReportingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private const int InitialPopulationSize = 50;
    private const long EvaluationBudget = 4000;

    [Fact]
    public async Task AnLShadeRunReportsTheActivePopulationShrinkingToItsMinimum()
    {
        var observer = await RunLShadeAsync();

        Assert.NotEmpty(observer.ReportedSizes);

        // Non-increasing: LPSR only ever drops individuals.
        for (int i = 1; i < observer.ReportedSizes.Count; i++)
        {
            Assert.True(
                observer.ReportedSizes[i] <= observer.ReportedSizes[i - 1],
                $"generation {i + 1} reported {observer.ReportedSizes[i]} after "
                + $"{observer.ReportedSizes[i - 1]}");
        }

        Assert.True(
            observer.ReportedSizes[0] < InitialPopulationSize,
            $"the first generation already reduces the population, but {observer.ReportedSizes[0]} was reported");
        Assert.Equal(LShadeStrategy.MinimumPopulationSize, observer.ReportedSizes[^1]);
    }

    [Fact]
    public async Task TheReportedIndividualsAreAllLive()
    {
        var observer = await RunLShadeAsync();

        // Every reported individual must be one the run is still evolving. Their fitness values
        // are within a couple of orders of magnitude of each other once L-SHADE has converged;
        // a stale leftover from an early generation is many orders away.
        var (size, ffValues) = observer.LastSnapshot;

        Assert.Equal(LShadeStrategy.MinimumPopulationSize, size);
        Assert.Equal(size, ffValues.Length);
        Assert.All(ffValues, ffValue => Assert.True(ffValue < 1E-6, $"stale-looking individual at {ffValue}"));
    }

    [Fact]
    public async Task CapacityKeepsReportingTheAllocatedLength()
    {
        var observer = await RunLShadeAsync();

        Assert.All(observer.ReportedCapacities, capacity => Assert.Equal(InitialPopulationSize, capacity));
    }

    [Fact]
    public async Task TheGenomeSizeDoesNotDriftWithTheShrinkingPopulation()
    {
        // GenomeSize is derived from the gene buffer, which is sized against the capacity; deriving
        // it from the active size instead would make it grow as the population shrinks.
        var observer = await RunLShadeAsync();

        Assert.All(observer.ReportedGenomeSizes, genomeSize => Assert.Equal(5, genomeSize));
    }

    private static async Task<SizeObserver> RunLShadeAsync()
    {
        var evaluator = new SphereEvaluator(dimension: 5);
        var observer = new SizeObserver();

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(InitialPopulationSize)
            .WithUniformPopulationSampling()
            .WithLShade(maxEvaluationNumber: EvaluationBudget)
            .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(EvaluationBudget))
            .UseProcessors(1)
            .WithPopulationUpdateHandler(observer)
            .Build();

        await de.RunAsync().WaitAsync(Timeout);

        return observer;
    }

    private sealed class SizeObserver : IPopulationUpdatedHandler
    {
        public List<int> ReportedSizes { get; } = [];

        public List<int> ReportedCapacities { get; } = [];

        public List<int> ReportedGenomeSizes { get; } = [];

        public (int Size, double[] FfValues) LastSnapshot { get; private set; } = (0, []);

        public void Handle(
            Population population)
        {
            ArgumentNullException.ThrowIfNull(population);

            var size = population.PopulationSize;
            ReportedSizes.Add(size);
            ReportedCapacities.Add(population.Capacity);
            ReportedGenomeSizes.Add(population.GenomeSize);

            var ffValues = new double[size];
            for (int i = 0; i < size; i++)
            {
                population.MoveCursorTo(i);
                ffValues[i] = population.IndividualCursor.FitnessFunctionValue;
            }

            LastSnapshot = (size, ffValues);
        }
    }
}
