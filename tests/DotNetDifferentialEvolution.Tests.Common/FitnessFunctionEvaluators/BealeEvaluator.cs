namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Beale function (2-D): a valley-shaped, non-separable function with sharp peaks at the
/// corners of the domain. Global minimum <c>f* = 0</c> at <c>(3, 0.5)</c>.
/// Domain [-4.5, 4.5]².
/// </summary>
public sealed class BealeEvaluator : BenchmarkFunctionEvaluator
{
    /// <summary>Initializes a new instance of the two-dimensional Beale function.</summary>
    public BealeEvaluator()
        : base(dimension: 2)
    {
    }

    /// <summary>Gets 2: the Beale function is defined in two dimensions only.</summary>
    protected override int MinimumDimension => 2;

    /// <summary>Evaluates the Beale function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];

        var a = 1.5 - x + x * y;
        var b = 2.25 - x + x * y * y;
        var c = 2.625 - x + x * y * y * y;
        return a * a + b * b + c * c;
    }

    /// <summary>Returns the lower bound of the Beale domain: -4.5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-4.5);

    /// <summary>Returns the upper bound of the Beale domain: 4.5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(4.5);

    /// <summary>Returns 0, the Beale global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Beale global minimizer, <c>(3, 0.5)</c>.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => new[] { 3.0, 0.5 };
}
