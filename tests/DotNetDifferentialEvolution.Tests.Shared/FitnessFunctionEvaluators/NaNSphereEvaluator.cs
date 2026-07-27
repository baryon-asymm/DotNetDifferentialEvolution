namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// A <see cref="SphereEvaluator"/> that returns <see cref="double.NaN"/> for a configured window
/// of evaluations, modelling a user objective that cannot produce a number for some inputs (a
/// diverging simulation, a domain error, ...). Counting evaluations instead of inspecting the
/// genes keeps the tests deterministic: the caller decides exactly which evaluations — the
/// initial population's, the trials', or both — come back as NaN. The counter is incremented
/// atomically so the window is well-defined under concurrent evaluation. Mirrors
/// <see cref="ExceptionRosenbrockEvaluator"/>, which throws on a chosen evaluation.
/// </summary>
public sealed class NaNSphereEvaluator : BenchmarkFunctionEvaluator
{
    private readonly SphereEvaluator _sphere;

    private int _evaluationsCount;

    /// <summary>Gets the first evaluation (1-based) that returns NaN.</summary>
    public int FirstNaNEvaluation { get; }

    /// <summary>Gets the last evaluation (1-based) that returns NaN.</summary>
    public int LastNaNEvaluation { get; }

    public NaNSphereEvaluator(
        int firstNaNEvaluation,
        int lastNaNEvaluation = int.MaxValue,
        int dimension = 2)
        : base(dimension)
    {
        _sphere = new SphereEvaluator(dimension);

        FirstNaNEvaluation = firstNaNEvaluation;
        LastNaNEvaluation = lastNaNEvaluation;
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var evaluationNumber = Interlocked.Increment(ref _evaluationsCount);
        var value = _sphere.Evaluate(genes);

        return evaluationNumber >= FirstNaNEvaluation && evaluationNumber <= LastNaNEvaluation
            ? double.NaN
            : value;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => _sphere.GetLowerBounds();

    public override ReadOnlyMemory<double> GetUpperBounds() => _sphere.GetUpperBounds();

    public override double GetGlobalMinimumFfValue() => _sphere.GetGlobalMinimumFfValue();

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => _sphere.GetGlobalMinimumGenes();
}
