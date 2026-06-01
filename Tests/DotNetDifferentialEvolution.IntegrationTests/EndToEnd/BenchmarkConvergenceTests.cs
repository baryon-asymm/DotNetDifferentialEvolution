using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end correctness: the full builder-driven optimizer must reach the known global
/// optimum on the standard benchmark functions, across unimodal / multimodal / separable /
/// non-separable landscapes. This is the evidence that the algorithms actually optimize, not
/// just that they run.
/// </summary>
[Trait("Category", "Integration")]
public class BenchmarkConvergenceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // ---- Unimodal: classic DE/rand/1/bin should reach a tight tolerance. ----

    [Theory]
    [InlineData("Sphere", 5)]
    [InlineData("Rosenbrock", 2)]
    [InlineData("Zakharov", 5)]
    [InlineData("SumOfDifferentPowers", 5)]
    [InlineData("DixonPrice", 2)]
    public async Task ClassicDe_ConvergesOnUnimodalFunctions(
        string functionName,
        int dimension)
    {
        var evaluator = BenchmarkFunctionCatalog.Create(functionName, dimension);

        var best = await BuilderOptimizer.BestOfAsync(attempts: 3, Timeout, () =>
            DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(60)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.6, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new StagnationStreakTerminationStrategy(2500, 1e-12))
                .UseProcessors(1)
                .Build());

        ConvergenceAssert.ReachedOptimum(evaluator, best, valueTolerance: 1e-6);
    }

    // ---- Multimodal: SHADE should reach the global basin (value-based). ----

    [Theory]
    [InlineData("Rastrigin", 2)]
    [InlineData("Ackley", 2)]
    [InlineData("Griewank", 2)]
    [InlineData("Levy", 2)]
    [InlineData("Himmelblau", 2)]
    [InlineData("Booth", 2)]
    [InlineData("Beale", 2)]
    public async Task Shade_ConvergesOnMultimodalFunctions(
        string functionName,
        int dimension)
    {
        var evaluator = BenchmarkFunctionCatalog.Create(functionName, dimension);

        var best = await BuilderOptimizer.BestOfAsync(attempts: 4, Timeout, () =>
            DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(50)
                .WithUniformPopulationSampling()
                .WithShade()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(3000))
                .UseProcessors(1)
                .Build());

        ConvergenceAssert.ReachedOptimum(evaluator, best, valueTolerance: 1e-4);
    }

    // ---- Deceptive / harder multimodal: give L-SHADE a real evaluation budget. ----

    [Theory]
    [Trait("Category", "Slow")]
    [InlineData("Schwefel", 2)]
    [InlineData("StyblinskiTang", 2)]
    [InlineData("Rastrigin", 5)]
    [InlineData("Ackley", 5)]
    public async Task LShade_ConvergesOnHarderMultimodalFunctions(
        string functionName,
        int dimension)
    {
        var evaluator = BenchmarkFunctionCatalog.Create(functionName, dimension);
        const long maxEvaluations = 300_000;

        var best = await BuilderOptimizer.BestOfAsync(attempts: 4, Timeout, () =>
            DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(100)
                .WithUniformPopulationSampling()
                .WithLShade(maxEvaluations)
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(maxEvaluations))
                .UseProcessors(1)
                .Build());

        // Looser tolerance: these landscapes are deceptive / dimension-scaled.
        var tolerance = 1e-2 * Math.Max(1.0, Math.Abs(evaluator.GetGlobalMinimumFfValue()));
        ConvergenceAssert.ReachedOptimum(evaluator, best, valueTolerance: tolerance);
    }
}
