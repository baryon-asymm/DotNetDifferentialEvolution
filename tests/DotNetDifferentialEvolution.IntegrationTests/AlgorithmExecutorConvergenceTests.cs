using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// Integration tests for the classic <see cref="AlgorithmExecutors.AlgorithmExecutor"/>
/// generation loop (mutation + selection + termination), driven single-threaded and seeded so
/// the runs are reproducible. Replaces the old random-genome-size AlgorithmExecutionTester.
/// </summary>
[Trait("Category", "Integration")]
public class AlgorithmExecutorConvergenceTests
{
    /// <summary>
    /// A seeded, single-threaded run of the classic loop reaches the 5-D Sphere optimum within 2000
    /// generations.
    /// </summary>
    [Fact]
    public void ConvergesOnSphere()
    {
        var evaluator = new SphereEvaluator(dimension: 5);
        var termination = new LimitGenerationNumberTerminationStrategy(2000);

        var result = ManualAlgorithmRunner.Run(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9, populationSize: 60, seed: 12345);

        ConvergenceAssert.ReachedOptimum(evaluator, result, valueTolerance: 1e-6, geneTolerance: 1e-3);
    }

    /// <summary>
    /// A seeded, single-threaded run of the classic loop follows the 2-D Rosenbrock valley to its
    /// optimum, stopping after a 2000-generation stagnation streak.
    /// </summary>
    [Fact]
    public void ConvergesOnRosenbrock()
    {
        var evaluator = new RosenbrockEvaluator(dimension: 2);
        var termination = new StagnationStreakTerminationStrategy(maxStagnationStreak: 2000, stagnationThreshold: 1e-9);

        var result = ManualAlgorithmRunner.Run(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9, populationSize: 100, seed: 999);

        ConvergenceAssert.ReachedOptimum(evaluator, result, valueTolerance: 1e-6, geneTolerance: 1e-3);
    }
}
