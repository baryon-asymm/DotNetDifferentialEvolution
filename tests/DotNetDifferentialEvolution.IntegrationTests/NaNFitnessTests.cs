using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// A NaN fitness value must never be absorbing. NaN loses every comparison it takes part in, so
/// without an explicit rule an individual whose objective returned NaN can neither be replaced by
/// a real-valued trial nor be displaced as the best individual — the run then reports NaN as its
/// result while the population holds perfectly good solutions. These tests pin the rule at every
/// place the engine compares fitness values: greedy selection, the per-worker scan, the
/// cross-worker reduction, the generation-strategy population scan, and the builder's pick of the
/// initial best.
/// </summary>
/// <remarks>
/// The scan tests run a single generation in which <em>every trial</em> evaluates to NaN, so no
/// trial is ever accepted and the population that the scans see is exactly the seeded one — NaNs
/// included. That isolates the scans from selection and keeps the assertions deterministic.
/// </remarks>
[Trait("Category", "Integration")]
public class NaNFitnessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task PerWorkerScan_DoesNotReportANaNIndividualAsTheBest()
    {
        // The worker's scan starts from the first individual it handles — index 0 for worker 0 —
        // which is the NaN one here, so it has to be displaced by a real-valued individual.
        const int populationSize = 8;
        var (context, executor) = CreateRunWithNaNTrials(populationSize, workersCount: 1);
        context.CurrentPopulation.FfValues.Span[0] = double.NaN;

        var result = await RunOneGenerationAsync(context, executor, workersCount: 1);

        Assert.True(double.IsNaN(FitnessAt(result, 0)));
        AssertBestIsTheLiveMinimum(result);
    }

    [Fact]
    public async Task CrossWorkerReduction_DoesNotReportANaNIndividualAsTheBest()
    {
        // Two workers stride the population: the master (worker 1) handles 1, 3, 5 — all NaN
        // here — and the slave (worker 0) handles 0, 2, 4. The reduction across workers starts
        // from the master's NaN, so the slave's real-valued best has to win.
        const int populationSize = 6;
        var (context, executor) = CreateRunWithNaNTrials(populationSize, workersCount: 2);
        var populationFfValues = context.CurrentPopulation.FfValues.Span;
        populationFfValues[1] = double.NaN;
        populationFfValues[3] = double.NaN;
        populationFfValues[5] = double.NaN;

        var result = await RunOneGenerationAsync(context, executor, workersCount: 2);

        Assert.True(double.IsNaN(FitnessAt(result, 1)));
        AssertBestIsTheLiveMinimum(result);
    }

    [Fact]
    public async Task PopulationScan_DoesNotReportANaNIndividualAsTheBest()
    {
        // With a generation strategy in play the orchestrator rescans the whole population
        // instead of reducing the per-worker indices.
        const int populationSize = 8;
        var (context, executor) = CreateRunWithNaNTrials(
            populationSize, workersCount: 1, generationStrategy: new NoOpGenerationStrategy());
        context.CurrentPopulation.FfValues.Span[0] = double.NaN;

        var result = await RunOneGenerationAsync(context, executor, workersCount: 1);

        Assert.True(double.IsNaN(FitnessAt(result, 0)));
        AssertBestIsTheLiveMinimum(result);
    }

    [Fact]
    public async Task PopulationScan_WithAnAllNaNPopulation_StillReportsAnInRangeIndex()
    {
        // Degenerate but reachable: nothing is better than anything else, and the scan still has
        // to name an individual rather than throw or return -1.
        const int populationSize = 8;
        var (context, executor) = CreateRunWithNaNTrials(
            populationSize, workersCount: 1, generationStrategy: new NoOpGenerationStrategy());
        context.CurrentPopulation.FfValues.Span.Fill(double.NaN);

        var result = await RunOneGenerationAsync(context, executor, workersCount: 1);

        Assert.InRange(result.BestIndividualIndex, 0, populationSize - 1);
        Assert.True(double.IsNaN(FitnessAt(result, result.BestIndividualIndex)));
    }

    [Fact]
    public async Task Builder_DoesNotHandANaNIndividualToMutationAsTheInitialBest()
    {
        // The builder evaluates the initial population in index order and picks the best from it;
        // the very first evaluation — individual 0 — comes back NaN.
        const int populationSize = 8;
        var evaluator = new NaNSphereEvaluator(firstNaNEvaluation: 1, lastNaNEvaluation: 1);
        var mutationStrategy = new BestIndexCapturingMutationStrategy();

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(populationSize)
            .WithUniformPopulationSampling()
            .WithMutationStrategy(mutationStrategy)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        await de.RunAsync().WaitAsync(Timeout);

        // Individual 0 is the NaN one, so it must not be the best the first generation mutates around.
        Assert.NotNull(mutationStrategy.FirstSeenBestIndividualIndex);
        Assert.NotEqual(0, mutationStrategy.FirstSeenBestIndividualIndex);
    }

    [Fact]
    public async Task JadeRun_WithANaNInTheInitialPopulation_ReportsAFiniteBest()
    {
        // The end-to-end symptom: individual 0 of the initial population evaluates to NaN, and
        // from then on both the greedy selection and the best-index scan refuse to let go of it.
        var evaluator = new NaNSphereEvaluator(firstNaNEvaluation: 1, lastNaNEvaluation: 1, dimension: 3);

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(20)
            .WithUniformPopulationSampling()
            .WithJade()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(300))
            .UseProcessors(1)
            .Build();

        var result = await de.RunAsync().WaitAsync(Timeout);

        AssertBestIsTheLiveMinimum(result);
    }

    /// <summary>
    /// Builds a one-generation run whose initial population is evaluated normally but whose every
    /// trial evaluates to NaN, so nothing is ever accepted and the seeded fitness values survive
    /// into the population the best-index scan works on.
    /// </summary>
    private static (ProblemContext Context, AlgorithmExecutor Executor) CreateRunWithNaNTrials(
        int populationSize,
        int workersCount,
        IGenerationStrategy? generationStrategy = null)
    {
        var evaluator = new NaNSphereEvaluator(firstNaNEvaluation: populationSize + 1);
        var termination = new LimitGenerationNumberTerminationStrategy(1);

        return ExecutorFactory.Create(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9,
            populationSize: populationSize, workersCount: workersCount, seed: null,
            generationStrategy: generationStrategy);
    }

    private static async Task<Population> RunOneGenerationAsync(
        ProblemContext context,
        AlgorithmExecutor executor,
        int workersCount)
    {
        using var harness = new MultiWorkerHarness(context, executor, workersCount);

        harness.StartAll();

        return await harness.Handler.GetResultPopulationTask().WaitAsync(Timeout);
    }

    /// <summary>
    /// Asserts that the reported best individual is the real-valued minimum of the population
    /// that was actually handed back (NaN entries never qualify).
    /// </summary>
    private static void AssertBestIsTheLiveMinimum(
        Population population)
    {
        var liveMinimum = double.PositiveInfinity;
        for (int i = 0; i < population.PopulationSize; i++)
        {
            var fitnessValue = FitnessAt(population, i);
            if (fitnessValue < liveMinimum)
                liveMinimum = fitnessValue;
        }

        population.MoveCursorToBestIndividual();

        Assert.False(double.IsNaN(population.IndividualCursor.FitnessFunctionValue));
        Assert.Equal(liveMinimum, population.IndividualCursor.FitnessFunctionValue);
    }

    private static double FitnessAt(
        Population population,
        int individualIndex)
    {
        population.MoveCursorTo(individualIndex);

        return population.IndividualCursor.FitnessFunctionValue;
    }

    /// <summary>
    /// A generation strategy that adapts nothing. Its only job is to be present, which is what
    /// makes the orchestrator determine the best individual by scanning the population.
    /// </summary>
    private sealed class NoOpGenerationStrategy : IGenerationStrategy
    {
        public void AfterGeneration(
            ProblemContext context,
            ReadOnlySpan<TrialRecord> trialRecords)
        {
        }
    }

    /// <summary>
    /// A mutation strategy that records the best-individual index the engine handed it for the
    /// very first trial — the index the builder computed from the initial population — and copies
    /// the parent unchanged so the run itself stays inert.
    /// </summary>
    private sealed class BestIndexCapturingMutationStrategy : IMutationStrategy
    {
        // It reads the best index but supplies nothing of its own, so it needs no control-parameter
        // provider — which is what lets the builder accept it on its own.
        public MutationRequirements Requirements => MutationRequirements.BestIndividual;

        public int? FirstSeenBestIndividualIndex { get; private set; }

        public void Mutate(
            in MutationContext context)
        {
            FirstSeenBestIndividualIndex ??= context.BestIndividualIndex;

            context.Population
                .Slice(context.IndividualIndex * context.GenomeSize, context.GenomeSize)
                .CopyTo(context.TrialIndividual);
        }
    }
}
