namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Levy function: multimodal, with the global minimum <c>f* = 0</c> at <c>(1, …, 1)</c>.
/// Built from the auxiliary variables <c>wᵢ = 1 + (xᵢ − 1)/4</c>. Domain [-10, 10]ⁿ.
/// </summary>
public sealed class LevyEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>Evaluates the Levy function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var n = genes.Length;

        static double W(double x) => 1.0 + (x - 1.0) / 4.0;
        static double Sin2(double v) => Math.Sin(v) * Math.Sin(v);

        var w1 = W(genes[0]);
        var result = Sin2(Math.PI * w1);

        for (var i = 0; i < n - 1; i++)
        {
            var wi = W(genes[i]);
            result += (wi - 1.0) * (wi - 1.0) * (1.0 + 10.0 * Sin2(Math.PI * wi + 1.0));
        }

        var wn = W(genes[n - 1]);
        result += (wn - 1.0) * (wn - 1.0) * (1.0 + Sin2(2.0 * Math.PI * wn));

        return result;
    }

    /// <summary>Returns the lower bound of the Levy domain: -10 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-10.0);

    /// <summary>Returns the upper bound of the Levy domain: 10 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(10.0);

    /// <summary>Returns 0, the Levy global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Levy global minimizer, <c>(1, …, 1)</c>.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(1.0);
}
