using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Base class for the standard global-optimization benchmark functions used to validate the
/// optimizers. Each function declares its search domain and known global optimum so a test
/// can assert that the algorithm actually reaches it.
/// </summary>
/// <remarks>
/// Formulas and optima follow the widely-used references: the SFU "Virtual Library of
/// Simulation Experiments" (https://www.sfu.ca/~ssurjano/optimization.html) and Naser et al.,
/// "A Review of Benchmark and Test Functions for Global Optimization Algorithms and
/// Metaheuristics", WIREs Computational Statistics, 2025. All functions are framed as
/// minimization problems (lower is better), matching <see cref="ITestFitnessFunctionEvaluator"/>.
/// </remarks>
public abstract class BenchmarkFunctionEvaluator : ITestFitnessFunctionEvaluator
{
    /// <summary>
    /// Initializes a new instance with the given number of dimensions.
    /// </summary>
    /// <param name="dimension">The number of decision variables (genome size).</param>
    protected BenchmarkFunctionEvaluator(
        int dimension)
    {
        if (dimension < MinimumDimension)
            throw new ArgumentOutOfRangeException(
                nameof(dimension),
                $"{Name} requires at least {MinimumDimension} dimension(s).");

        Dimension = dimension;
    }

    /// <summary>Gets the human-readable name of the function (used in test output).</summary>
    public virtual string Name => GetType().Name.Replace("Evaluator", string.Empty, StringComparison.Ordinal);

    /// <summary>Gets the number of decision variables.</summary>
    public int Dimension { get; }

    /// <summary>Gets the smallest dimension the function is defined for.</summary>
    protected virtual int MinimumDimension => 1;

    /// <inheritdoc />
    public abstract double Evaluate(
        ReadOnlySpan<double> genes);

    /// <inheritdoc />
    public double Evaluate(
        int workerIndex,
        ReadOnlySpan<double> genes) => Evaluate(genes);

    /// <inheritdoc />
    public abstract ReadOnlyMemory<double> GetLowerBounds();

    /// <inheritdoc />
    public abstract ReadOnlyMemory<double> GetUpperBounds();

    /// <inheritdoc />
    public abstract double GetGlobalMinimumFfValue();

    /// <summary>
    /// Returns the global minimizer x*. The default throws: several benchmark functions have
    /// multiple global minimizers (e.g. Himmelblau) or minimizers that are numerically
    /// awkward to land on exactly (e.g. Schwefel), so those are validated on the fitness
    /// value alone. Functions with a single, clean minimizer override this.
    /// </summary>
    public virtual ReadOnlyMemory<double> GetGlobalMinimumGenes() =>
        throw new NotSupportedException(
            $"{Name} does not expose a single closed-form minimizer; assert on the fitness value instead.");

    /// <summary>Builds a <see cref="Dimension"/>-length bound vector filled with <paramref name="value"/>.</summary>
    protected ReadOnlyMemory<double> UniformBounds(
        double value)
    {
        var bounds = new double[Dimension];
        Array.Fill(bounds, value);
        return bounds;
    }

    /// <summary>Builds a <see cref="Dimension"/>-length minimizer filled with <paramref name="value"/>.</summary>
    protected ReadOnlyMemory<double> UniformMinimizer(
        double value) => UniformBounds(value);
}
