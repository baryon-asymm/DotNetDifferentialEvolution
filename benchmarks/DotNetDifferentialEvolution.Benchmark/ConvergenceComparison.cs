using DotNetDifferentialEvolution.Benchmark.Functions;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;

// DifferentialEvolutionBuilder and its staged interfaces (IMutationStrategyRequired,
// IPopulationSamplingRequired, ...) live in the root DotNetDifferentialEvolution namespace.
using DotNetDifferentialEvolution;

namespace DotNetDifferentialEvolution.Benchmark;

/// <summary>
/// Runs every DE variant against the same multimodal problems under an equal
/// fitness-evaluation budget and prints the best objective value each one reaches. This is
/// a convergence-quality comparison (lower is better), complementing the throughput
/// micro-benchmark. Invoke with <c>dotnet run -c Release -- convergence</c>.
/// </summary>
public static class ConvergenceComparison
{
    private const int Dimensions = 30;
    private const long MaxEvaluationNumber = 300_000;
    private const int FixedPopulationSize = 100;

    private sealed record Variant(string Name, Func<IFitnessFunctionEvaluator, double[], double[], DifferentialEvolution> Build);

    private sealed record Problem(string Name, IFitnessFunctionEvaluator Evaluator, double LowerBound, double UpperBound);

    public static void Run()
    {
        var problems = new[]
        {
            new Problem("Rastrigin", new RastriginEvaluator(), -5.12, 5.12),
            new Problem("Ackley", new AckleyEvaluator(), -32.768, 32.768),
        };

        var variants = new[]
        {
            new Variant("DE/rand/1/bin", (e, l, u) => Common(e, l, u, FixedPopulationSize)
                .WithDefaultMutationStrategy(0.5, 0.9).WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(MaxEvaluationNumber))
                .UseAllProcessors().Build()),
            new Variant("jDE", (e, l, u) => Common(e, l, u, FixedPopulationSize)
                .WithJde()
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(MaxEvaluationNumber))
                .UseAllProcessors().Build()),
            new Variant("JADE", (e, l, u) => Common(e, l, u, FixedPopulationSize)
                .WithJade()
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(MaxEvaluationNumber))
                .UseAllProcessors().Build()),
            new Variant("SHADE", (e, l, u) => Common(e, l, u, FixedPopulationSize)
                .WithShade()
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(MaxEvaluationNumber))
                .UseAllProcessors().Build()),
            new Variant("L-SHADE", (e, l, u) => Common(e, l, u, 18 * Dimensions)
                .WithLShade(MaxEvaluationNumber)
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(MaxEvaluationNumber))
                .UseAllProcessors().Build()),
        };

        Console.WriteLine($"Convergence comparison — {Dimensions}D, budget {MaxEvaluationNumber:N0} evaluations (best objective, lower is better)");
        Console.WriteLine();
        Console.Write($"{"Variant",-16}");
        foreach (var problem in problems)
            Console.Write($"{problem.Name,16}");
        Console.WriteLine();

        foreach (var variant in variants)
        {
            Console.Write($"{variant.Name,-16}");
            foreach (var problem in problems)
            {
                var bounds = CreateBounds(problem.LowerBound, problem.UpperBound);
                using var de = variant.Build(problem.Evaluator, bounds.lower, bounds.upper);
                var result = de.RunAsync().GetAwaiter().GetResult();
                result.MoveCursorToBestIndividual();
                Console.Write($"{result.IndividualCursor.FitnessFunctionValue,16:E3}");
            }

            Console.WriteLine();
        }
    }

    private static IMutationStrategyRequired Common(
        IFitnessFunctionEvaluator evaluator,
        double[] lowerBound,
        double[] upperBound,
        int populationSize)
        => DifferentialEvolutionBuilder
            .ForFunction(evaluator)
            .WithBounds(lowerBound, upperBound)
            .WithPopulationSize(populationSize)
            .WithUniformPopulationSampling();

    private static (double[] lower, double[] upper) CreateBounds(
        double lowerBound,
        double upperBound)
    {
        var lower = new double[Dimensions];
        var upper = new double[Dimensions];
        for (int i = 0; i < Dimensions; i++)
        {
            lower[i] = lowerBound;
            upper[i] = upperBound;
        }

        return (lower, upper);
    }
}
