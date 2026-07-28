using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.LocalSearch;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end coverage for the memetic local-search hook (<c>WithLocalSearch</c> /
/// <see cref="ILocalSearchRefiner"/>): it fires on the configured generation cadence, its in-place
/// writes to the population survive into the result, and its evaluations are folded into the run's
/// evaluation count.
/// </summary>
[Trait("Category", "Integration")]
public class LocalSearchHookTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Refiner_RunsOnConfiguredCadence_AndWriteBackSurvivesIntoResult()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var refiner = new SnapToOriginRefiner(evaluationsPerCall: 7);

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(12)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(6))
            .UseProcessors(1)
            .WithLocalSearch(refiner, everyNGenerations: 2)
            .Build();

        var result = await de.RunAsync().WaitAsync(Timeout);
        result.MoveCursorToBestIndividual();

        // Cadence: 6 generations, every 2nd → fired at 2, 4, 6.
        Assert.Equal(new[] { 2, 4, 6 }, refiner.Generations);

        // Write-back: the refiner snapped the best to the Sphere optimum (origin, value 0), and
        // that survived selection and termination into the final result.
        Assert.Equal(0.0, result.IndividualCursor.FitnessFunctionValue);
        foreach (var gene in result.IndividualCursor.Genes.Span)
            Assert.Equal(0.0, gene);
    }

    [Fact]
    public async Task Refiner_EvaluationsAreFoldedIntoEvaluationCount()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        const int populationSize = 12;
        const int generations = 5;
        const int evaluationsPerCall = 7;
        var refiner = new SnapToOriginRefiner(evaluationsPerCall);

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(populationSize)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(generations))
            .UseProcessors(1)
            .WithLocalSearch(refiner, everyNGenerations: 1)
            .Build();

        var result = await de.RunAsync().WaitAsync(Timeout);

        // Initial population evaluation + one trial per individual per generation + the refiner's
        // own evaluations on every generation (it runs every generation here).
        var expected = (long)populationSize                       // initial population
                       + (long)populationSize * generations       // DE trials
                       + (long)evaluationsPerCall * generations;  // local search
        Assert.Equal(expected, result.EvaluationCount);
    }

    /// <summary>
    /// A test refiner that snaps the best individual to the origin (the Sphere optimum, value 0),
    /// records the generations it ran on, and reports a fixed number of evaluations per call.
    /// </summary>
    private sealed class SnapToOriginRefiner : ILocalSearchRefiner
    {
        private readonly int _evaluationsPerCall;

        public SnapToOriginRefiner(int evaluationsPerCall) => _evaluationsPerCall = evaluationsPerCall;

        public List<int> Generations { get; } = [];

        public void Refine(ProblemContext context, int generationNumber)
        {
            ArgumentNullException.ThrowIfNull(context);

            var genomeSize = context.GenomeSize;
            var best = context.BestIndividualIndex;

            context.CurrentPopulation.Genes.Span.Slice(best * genomeSize, genomeSize).Clear(); // origin
            context.CurrentPopulation.FfValues.Span[best] = 0.0;
            context.EvaluationCount += _evaluationsPerCall;

            Generations.Add(generationNumber);
        }
    }
}
