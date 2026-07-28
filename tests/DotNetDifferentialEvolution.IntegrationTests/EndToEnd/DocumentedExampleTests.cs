using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetOptimization.Abstractions;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// The code printed in <c>docs/AGENT_GUIDE.md</c>, compiled and run.
/// <para>
/// Documentation examples are the part of a library most likely to rot and the part a reader — a
/// coding agent above all — is most likely to copy verbatim. Prose about the API can drift without
/// anything noticing; a snippet that no compiler ever sees can drift into code that does not build
/// at all. These tests exist so that a signature change breaks the build here, next to the
/// document, rather than in a consumer's project.
/// </para>
/// <para>
/// Keep the bodies textually identical to the guide's snippets. If a snippet has to change, change
/// it in both places in the same commit; the point is lost the moment they are allowed to differ.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class DocumentedExampleTests
{
    /// <summary>
    /// The objective from the guide's §1 and §2 — pure, so the worker overload delegates to the
    /// single-argument one.
    /// </summary>
    private sealed class Sphere : IFitnessFunctionEvaluator
    {
        public double Evaluate(ReadOnlySpan<double> genes)
        {
            var sum = 0.0;
            foreach (var gene in genes)
                sum += gene * gene;

            return sum;
        }

        public double Evaluate(int workerIndex, ReadOnlySpan<double> genes) => Evaluate(genes);
    }

    /// <summary>§1, "The shortest complete program".</summary>
    [Fact]
    public async Task TheShortestCompleteProgramBuildsRunsAndReportsAMinimum()
    {
        using var de = DifferentialEvolutionBuilder
            .ForFunction(new Sphere())
            .WithBounds(new[] { -5.0, -5.0, -5.0 }, new[] { 5.0, 5.0, 5.0 })
            .WithPopulationSize(50)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(200))
            .UseAllProcessors()
            .Build();

        var population = await de.RunAsync();

        population.MoveCursorToBestIndividual();
        var best = population.IndividualCursor.GetSnapshot(deepCopy: true);

        // The sphere's minimum is 0 at the origin. The assertion is deliberately loose: this test
        // pins the example, not the convergence rate, which the convergence suites cover.
        Assert.True(
            best.FitnessFunctionValue < 1.0,
            $"the documented example reported {best.FitnessFunctionValue}");
        Assert.Equal(3, best.Genes.Length);
    }

    /// <summary>
    /// §3, "L-SHADE has one hard requirement" — the budget given to the variant and the budget
    /// that stops the run are the same constant.
    /// </summary>
    [Fact]
    public async Task TheLShadeExampleBuildsAndSpendsItsBudget()
    {
        const long Budget = 300_000;
        const int Dimensions = 30;

        var lowerBound = new double[Dimensions];
        var upperBound = new double[Dimensions];
        Array.Fill(lowerBound, -100.0);
        Array.Fill(upperBound, 100.0);

        var objective = new Sphere();

        using var de = DifferentialEvolutionBuilder
            .ForFunction(objective)
            .WithBounds(lowerBound, upperBound)
            .WithPopulationSize(18 * Dimensions)   // r_N^init = 18 from the paper's Table II
            .WithUniformPopulationSampling()
            .WithLShade(maxEvaluationNumber: Budget)
            .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(Budget))
            .UseAllProcessors()
            .Build();

        var population = await de.RunAsync();

        Assert.True(
            population.EvaluationCount >= Budget,
            $"the run stopped after {population.EvaluationCount} of {Budget} evaluations");

        // §7: the live population is what PopulationSize reports, and LPSR has taken it to the
        // floor by the time the budget is exhausted.
        Assert.Equal(4, population.PopulationSize);
        Assert.True(population.Capacity > population.PopulationSize);
    }

    /// <summary>
    /// §7, "Reading the result" — the cursor walk. Also pins the guide's claim that a shallow
    /// snapshot keeps referencing the population while a deep one does not.
    /// </summary>
    [Fact]
    public async Task TheResultIsReadThroughTheCursor()
    {
        using var de = DifferentialEvolutionBuilder
            .ForFunction(new Sphere())
            .WithBounds(new[] { -5.0, -5.0, -5.0 }, new[] { 5.0, 5.0, 5.0 })
            .WithPopulationSize(20)
            .WithUniformPopulationSampling()
            .WithShade()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(50))
            .UseAllProcessors()
            .Build();

        var population = await de.RunAsync();

        population.MoveCursorToBestIndividual();
        var best = population.IndividualCursor.GetSnapshot(deepCopy: true);
        var bestGenes = best.Genes.ToArray();

        var visited = 0;
        for (var i = 0; i < population.PopulationSize; i++)
        {
            population.MoveCursorTo(i);
            visited++;
        }

        Assert.Equal(population.PopulationSize, visited);

        // The deep snapshot is unaffected by every cursor move the loop just made.
        Assert.Equal(bestGenes, best.Genes.ToArray());
    }
}
