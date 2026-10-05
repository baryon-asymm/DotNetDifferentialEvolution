using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// A linear test objective, the sum of the genes, over caller-supplied bounds. Being
/// monotonic in every gene, it is minimized at the lower bounds, which makes its optimum
/// trivial to state for any domain.
/// </summary>
public class SimpleSumEvaluator : ITestFitnessFunctionEvaluator
{
    private readonly ReadOnlyMemory<double> _lowerBounds;
    private readonly ReadOnlyMemory<double> _upperBounds;

    /// <summary>Initializes a new instance over the given domain.</summary>
    /// <param name="lowerBounds">The lower bound of each gene.</param>
    /// <param name="upperBounds">The upper bound of each gene.</param>
    /// <exception cref="ArgumentException">The bounds have different lengths.</exception>
    public SimpleSumEvaluator(
        ReadOnlyMemory<double> lowerBounds,
        ReadOnlyMemory<double> upperBounds)
    {
        if (lowerBounds.Length != upperBounds.Length)
        {
            throw new ArgumentException("Lower and upper bounds must have the same length.");
        }

        _lowerBounds = lowerBounds;
        _upperBounds = upperBounds;
    }

    /// <summary>Returns the sum of <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate.</param>
    /// <returns>The sum of the genes.</returns>
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;

        foreach (var gene in genes)
        {
            sum += gene;
        }

        return sum;
    }

    /// <summary>Returns the sum of <paramref name="genes"/>; the worker index is ignored.</summary>
    /// <param name="workerIndex">The index of the calling worker; unused.</param>
    /// <param name="genes">The point to evaluate.</param>
    /// <returns>The sum of the genes.</returns>
    public double Evaluate(
        int workerIndex,
        ReadOnlySpan<double> genes) => Evaluate(genes);

    /// <summary>Returns the lower bounds passed to the constructor.</summary>
    /// <returns>One bound per gene.</returns>
    public ReadOnlyMemory<double> GetLowerBounds() => _lowerBounds;

    /// <summary>Returns the upper bounds passed to the constructor.</summary>
    /// <returns>One bound per gene.</returns>
    public ReadOnlyMemory<double> GetUpperBounds() => _upperBounds;

    /// <summary>Returns the global minimum value, the sum of the lower bounds.</summary>
    /// <returns>The sum of the lower bounds.</returns>
    public double GetGlobalMinimumFfValue() => _lowerBounds.ToArray().Sum();

    /// <summary>Returns the global minimizer, the lower bounds.</summary>
    /// <returns>The lower bounds.</returns>
    public ReadOnlyMemory<double> GetGlobalMinimumGenes() => _lowerBounds;
}
