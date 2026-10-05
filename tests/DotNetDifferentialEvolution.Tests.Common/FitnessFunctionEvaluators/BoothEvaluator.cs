namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Booth function (2-D): <c>f(x, y) = (x + 2y − 7)² + (2x + y − 5)²</c>. A simple
/// bowl-shaped, non-separable function. Global minimum <c>f* = 0</c> at <c>(1, 3)</c>.
/// Domain [-10, 10]².
/// </summary>
public sealed class BoothEvaluator : BenchmarkFunctionEvaluator
{
    /// <summary>Initializes a new instance of the two-dimensional Booth function.</summary>
    public BoothEvaluator()
        : base(dimension: 2)
    {
    }

    /// <summary>Gets 2: the Booth function is defined in two dimensions only.</summary>
    protected override int MinimumDimension => 2;

    /// <summary>Evaluates the Booth function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];

        var a = x + 2.0 * y - 7.0;
        var b = 2.0 * x + y - 5.0;
        return a * a + b * b;
    }

    /// <summary>Returns the lower bound of the Booth domain: -10 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-10.0);

    /// <summary>Returns the upper bound of the Booth domain: 10 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(10.0);

    /// <summary>Returns 0, the Booth global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Booth global minimizer, <c>(1, 3)</c>.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => new[] { 1.0, 3.0 };
}
