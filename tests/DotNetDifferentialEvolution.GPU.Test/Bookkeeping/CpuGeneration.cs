using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// The CPU package's side of the bookkeeping parity checks (ACCEPTANCE.md, S7, S8): a <see cref="GenerationContext"/>
/// over one-gene individuals, with an archive, as the CPU engine hands it to a generation strategy, and the trial records
/// it is given.
/// </summary>
internal static class CpuGeneration
{
    /// <summary>A generation context of N individuals, D genes, the parents given, an archive of the capacity and size given.</summary>
    /// <param name="parents">The discarded parents, individual-major (the CPU engine's trial population after the swap).</param>
    /// <param name="parentFitness">Their fitness values.</param>
    /// <param name="genomeSize">D.</param>
    /// <param name="archive">The archive's buffer, capacity·D.</param>
    /// <param name="archiveCapacity">The capacity.</param>
    /// <param name="archiveSize">The size before the generation.</param>
    /// <returns>The context, and the problem context behind it to read the archive's size back.</returns>
    public static (GenerationContext Generation, ProblemContext Problem) Context(
        double[] parents, double[] parentFitness, int genomeSize, double[] archive, int archiveCapacity, int archiveSize)
    {
        var populationSize = parentFitness.Length;
        var bounds = new double[genomeSize];
        var problem = new ProblemContext(
            populationSize,
            genomeSize,
            1,
            bounds,
            bounds,
            new NoObjective(),
            new LimitGenerationNumberTerminationStrategy(1),
            new double[populationSize * genomeSize],
            new double[populationSize],
            parents,
            parentFitness)
        {
            MutationRequirements = MutationRequirements.FitnessRanking,
            Archive = archive,
            ArchiveCapacity = archiveCapacity,
            ArchiveSize = archiveSize,
        };
        return (new GenerationContext(problem), problem);
    }

    /// <summary>A trial record.</summary>
    /// <param name="outcome">The GPU's outcome code.</param>
    /// <param name="mutationForce">F.</param>
    /// <param name="crossoverProbability">CR.</param>
    /// <param name="parentFitness">f(parent).</param>
    /// <param name="trialFitness">f(trial).</param>
    /// <returns>The CPU package's record.</returns>
    public static TrialRecord Record(int outcome, double mutationForce, double crossoverProbability, double parentFitness, double trialFitness) => new()
    {
        Outcome = outcome switch
        {
            GPU.Kernels.Selection.Improved => SelectionOutcome.TrialImproved,
            GPU.Kernels.Selection.Accepted => SelectionOutcome.TrialAccepted,
            _ => SelectionOutcome.ParentKept,
        },
        UsedF = mutationForce,
        UsedCr = crossoverProbability,
        ParentFfValue = parentFitness,
        TrialFfValue = trialFitness,
    };

    /// <summary>
    /// A CPU-package random provider that returns scripted values, for reading a strategy's state back through its
    /// <c>GetControlParameters</c>: an index draw returns the next scripted index, a double draw the next scripted double.
    /// </summary>
    /// <param name="indices">The indices, in order.</param>
    /// <param name="doubles">The doubles, in order.</param>
    internal sealed class ScriptedProvider(int[] indices, double[] doubles) : BaseRandomProvider
    {
        private int _index;
        private int _double;

        /// <inheritdoc />
        public override int Next(int maxValue) => indices[_index++];

        /// <inheritdoc />
        public override double NextDouble() => doubles[_double++];
    }

    /// <summary>The objective the context requires and the bookkeeping never calls.</summary>
    private sealed class NoObjective : IFitnessFunctionEvaluator
    {
        public double Evaluate(ReadOnlySpan<double> genes) => throw new InvalidOperationException("The bookkeeping evaluates nothing.");

        public double Evaluate(int workerIndex, ReadOnlySpan<double> genes) => Evaluate(genes);
    }
}
