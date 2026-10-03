namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Himmelblau function (2-D): <c>f(x, y) = (x² + y − 11)² + (x + y² − 7)²</c>. Multimodal
/// with <b>four</b> equal global minima (all with value 0), e.g. (3, 2). Useful for checking
/// that the optimizer can settle into any one global basin. Domain [-5, 5]². Validated on the
/// fitness value, since the minimizer is not unique.
/// </summary>
public sealed class HimmelblauEvaluator : BenchmarkFunctionEvaluator
{
    /// <summary>Initializes a new instance of the two-dimensional Himmelblau function.</summary>
    public HimmelblauEvaluator()
        : base(dimension: 2)
    {
    }

    /// <summary>Gets 2: the Himmelblau function is defined in two dimensions only.</summary>
    protected override int MinimumDimension => 2;

    /// <summary>Evaluates the Himmelblau function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];

        var a = x * x + y - 11.0;
        var b = x + y * y - 7.0;
        return a * a + b * b;
    }

    /// <summary>Returns the lower bound of the Himmelblau domain: -5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    /// <summary>Returns the upper bound of the Himmelblau domain: 5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.0);

    /// <summary>Returns 0, the value of each of the four Himmelblau global minima.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;
}
