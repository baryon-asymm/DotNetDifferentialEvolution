namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// A <see cref="SphereEvaluator"/> that returns <see cref="double.NaN"/> for a configured window
/// of evaluations, modelling a user objective that cannot produce a number for some inputs (a
/// diverging simulation, a domain error, ...). Counting evaluations instead of inspecting the
/// genes keeps the tests deterministic: the caller decides exactly which evaluations — the
/// initial population's, the trials', or both — come back as NaN. The counter is incremented
/// atomically so the window is well-defined under concurrent evaluation. Mirrors
/// <see cref="ExceptionRosenbrockEvaluator"/>, which throws on a chosen evaluation.
/// </summary>
public sealed class NaNSphereEvaluator(
    int firstNaNEvaluation,
    int lastNaNEvaluation = int.MaxValue,
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    private readonly SphereEvaluator _sphere = new(dimension);

    private int _evaluationsCount;

    /// <summary>Gets the first evaluation (1-based) that returns NaN.</summary>
    public int FirstNaNEvaluation { get; } = firstNaNEvaluation;

    /// <summary>Gets the last evaluation (1-based) that returns NaN.</summary>
    public int LastNaNEvaluation { get; } = lastNaNEvaluation;

    /// <summary>
    /// Evaluates the sphere function at <paramref name="genes"/>, or returns NaN when this
    /// evaluation's 1-based number lies in [<see cref="FirstNaNEvaluation"/>, <see cref="LastNaNEvaluation"/>].
    /// </summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The sphere value at <paramref name="genes"/>, or <see cref="double.NaN"/> inside the window.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var evaluationNumber = Interlocked.Increment(ref _evaluationsCount);
        var value = _sphere.Evaluate(genes);

        return evaluationNumber >= FirstNaNEvaluation && evaluationNumber <= LastNaNEvaluation
            ? double.NaN
            : value;
    }

    /// <summary>Returns the lower bound of the sphere domain: -5.12 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => _sphere.GetLowerBounds();

    /// <summary>Returns the upper bound of the sphere domain: 5.12 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => _sphere.GetUpperBounds();

    /// <summary>Returns 0, the sphere global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => _sphere.GetGlobalMinimumFfValue();

    /// <summary>Returns the sphere global minimizer, the origin.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => _sphere.GetGlobalMinimumGenes();
}
