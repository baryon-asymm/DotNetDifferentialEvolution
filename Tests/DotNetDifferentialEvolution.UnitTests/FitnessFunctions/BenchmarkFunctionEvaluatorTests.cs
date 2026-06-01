using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.UnitTests.FitnessFunctions;

/// <summary>
/// Validates the benchmark function library itself — the correctness backbone of the
/// convergence tests. If a formula or declared optimum were wrong, the convergence tests would
/// be meaningless, so each function is checked at its known minimizer and for well-formed bounds.
/// </summary>
[Trait("Category", "Unit")]
public class BenchmarkFunctionEvaluatorTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void EvaluatingAtTheKnownMinimizerReproducesTheGlobalMinimum(
        int dimension)
    {
        foreach (var evaluator in BenchmarkFunctionCatalog.WithKnownMinimizer(dimension))
        {
            var minimizer = evaluator.GetGlobalMinimumGenes();
            var expected = evaluator.GetGlobalMinimumFfValue();
            var actual = evaluator.Evaluate(minimizer.Span);

            // Absolute floor plus a small relative term (Styblinski-Tang's optimum is an
            // approximate, dimension-scaled negative value; the rest are exactly zero).
            var tolerance = 1e-6 + 1e-4 * Math.Abs(expected);
            Assert.True(
                Math.Abs(actual - expected) <= tolerance,
                $"{evaluator.Name} at its minimizer: expected {expected} (±{tolerance}), got {actual}.");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void DeclaresWellFormedBounds(
        int dimension)
    {
        foreach (var name in new[]
                 {
                     "Sphere", "Rosenbrock", "Zakharov", "SumOfDifferentPowers", "DixonPrice",
                     "Rastrigin", "Ackley", "Griewank", "Levy", "StyblinskiTang", "Schwefel",
                     "Booth", "Beale", "Himmelblau",
                 })
        {
            var evaluator = BenchmarkFunctionCatalog.Create(name, dimension);
            var lower = evaluator.GetLowerBounds();
            var upper = evaluator.GetUpperBounds();

            Assert.Equal(lower.Length, upper.Length);
            Assert.Equal(evaluator.Dimension, lower.Length);
            for (int i = 0; i < lower.Length; i++)
                Assert.True(lower.Span[i] < upper.Span[i], $"{evaluator.Name} bound {i} must be a proper interval.");
        }
    }

    [Fact]
    public void WorkerIndexedEvaluateMatchesPlainEvaluate()
    {
        var evaluator = new RastriginEvaluator(dimension: 3);
        double[] genes = [0.4, -1.2, 2.7];

        Assert.Equal(evaluator.Evaluate(genes), evaluator.Evaluate(workerIndex: 5, genes));
    }
}
