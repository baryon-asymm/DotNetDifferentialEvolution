namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Booth function (2-D): <c>f(x, y) = (x + 2y − 7)² + (2x + y − 5)²</c>. A simple
/// bowl-shaped, non-separable function. Global minimum <c>f* = 0</c> at <c>(1, 3)</c>.
/// Domain [-10, 10]².
/// </summary>
public sealed class BoothEvaluator : BenchmarkFunctionEvaluator
{
    public BoothEvaluator()
        : base(dimension: 2)
    {
    }

    protected override int MinimumDimension => 2;

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];

        var a = x + 2.0 * y - 7.0;
        var b = 2.0 * x + y - 5.0;
        return a * a + b * b;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-10.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(10.0);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => new[] { 1.0, 3.0 };
}
