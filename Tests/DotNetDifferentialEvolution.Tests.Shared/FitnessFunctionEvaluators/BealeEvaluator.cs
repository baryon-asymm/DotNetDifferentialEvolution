namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Beale function (2-D): a valley-shaped, non-separable function with sharp peaks at the
/// corners of the domain. Global minimum <c>f* = 0</c> at <c>(3, 0.5)</c>.
/// Domain [-4.5, 4.5]².
/// </summary>
public sealed class BealeEvaluator : BenchmarkFunctionEvaluator
{
    public BealeEvaluator()
        : base(dimension: 2)
    {
    }

    protected override int MinimumDimension => 2;

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

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-4.5);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(4.5);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => new[] { 3.0, 0.5 };
}
