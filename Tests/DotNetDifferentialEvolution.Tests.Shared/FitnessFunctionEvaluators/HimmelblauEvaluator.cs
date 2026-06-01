namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Himmelblau function (2-D): <c>f(x, y) = (x² + y − 11)² + (x + y² − 7)²</c>. Multimodal
/// with <b>four</b> equal global minima (all with value 0), e.g. (3, 2). Useful for checking
/// that the optimizer can settle into any one global basin. Domain [-5, 5]². Validated on the
/// fitness value, since the minimizer is not unique.
/// </summary>
public sealed class HimmelblauEvaluator : BenchmarkFunctionEvaluator
{
    public HimmelblauEvaluator()
        : base(dimension: 2)
    {
    }

    protected override int MinimumDimension => 2;

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];

        var a = x * x + y - 11.0;
        var b = x + y * y - 7.0;
        return a * a + b * b;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.0);

    public override double GetGlobalMinimumFfValue() => 0.0;
}
