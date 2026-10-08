using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.TerminationStrategies;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// An objective that scores most of its domain <see cref="double.MaxValue"/> (the usual "infeasible"
/// sentinel) must not drive SHADE or L-SHADE into <c>NaN</c> trial vectors: improvements over a
/// sentinel parent are finite and huge, and their sums used to overflow into <c>NaN</c> in the
/// memory (criterion O3 of <c>Algorithms/Shade</c>).
/// </summary>
[Trait("Category", "Integration")]
public class SentinelFitnessTests
{
    private const int Dimension = 8;
    private const int PopulationSize = 100;
    private const long MaxEvaluations = 20_000;
    private const int Seed = 12345;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// With the feasible region <c>x0 &lt; -4</c> and the sentinel everywhere else, a seeded
    /// single-worker run of 20 000 evaluations is never handed a vector with a <c>NaN</c> gene, and
    /// its best individual has none.
    /// </summary>
    /// <param name="variant">The variant to run: <c>SHADE</c> or <c>L-SHADE</c>.</param>
    [Theory]
    [InlineData("SHADE")]
    [InlineData("L-SHADE")]
    public async Task TheObjectiveIsNeverGivenANaNGeneWhenMostParentsScoreTheSentinel(
        string variant)
    {
        var objective = new SentinelSphere();
        double[] lowerBound = [.. Enumerable.Repeat(-5.0, Dimension)];
        double[] upperBound = [.. Enumerable.Repeat(5.0, Dimension)];

        var best = await BuilderOptimizer.RunOnceAsync(Timeout, () =>
        {
            var withSampling = DifferentialEvolutionBuilder.ForFunction(objective)
                .WithBounds(lowerBound, upperBound)
                .WithPopulationSize(PopulationSize)
                .WithUniformPopulationSampling();

            var configured = variant switch
            {
                "SHADE" => withSampling.WithShade(0.2, 1.0, 100),
                "L-SHADE" => withSampling.WithLShade(MaxEvaluations, 0.11, 2.6, 6),
                _ => throw new ArgumentOutOfRangeException(nameof(variant)),
            };

            return configured
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(MaxEvaluations))
                .UseProcessors(1)
                .WithSeed(Seed)
                .Build();
        }).ConfigureAwait(true);

        var bestGenes = best.IndividualCursor.Genes.Span;
        var bestGenesWithNaN = 0;
        foreach (var gene in bestGenes)
        {
            if (double.IsNaN(gene))
            {
                bestGenesWithNaN++;
            }
        }

        Assert.True(
            objective.EvaluationsWithNaNGene == 0,
            $"{variant}: {objective.EvaluationsWithNaNGene} of {objective.Evaluations} evaluations were given a NaN gene");
        Assert.Equal(0, bestGenesWithNaN);
    }

    /// <summary>
    /// An 8-D sphere that is feasible only where <c>x0 &lt; -4</c> and scores
    /// <see cref="double.MaxValue"/> elsewhere. It counts the vectors it is given with a
    /// <c>NaN</c> gene and scores such a gene as 0, so that a vector with one would beat the
    /// honest ones and show in the best individual.
    /// </summary>
    private sealed class SentinelSphere : IFitnessFunctionEvaluator
    {
        private int _evaluations;
        private int _evaluationsWithNaNGene;

        public int Evaluations => Volatile.Read(ref _evaluations);

        public int EvaluationsWithNaNGene => Volatile.Read(ref _evaluationsWithNaNGene);

        public double Evaluate(ReadOnlySpan<double> genes)
        {
            _ = Interlocked.Increment(ref _evaluations);

            var hasNaNGene = false;
            var sum = 0.0;
            foreach (var gene in genes)
            {
                if (double.IsNaN(gene))
                {
                    hasNaNGene = true;
                    continue;
                }

                sum += gene * gene;
            }

            if (hasNaNGene)
            {
                _ = Interlocked.Increment(ref _evaluationsWithNaNGene);
            }

            return genes[0] < -4.0 ? sum : double.MaxValue;
        }

        public double Evaluate(int workerIndex, ReadOnlySpan<double> genes) => Evaluate(genes);
    }
}
