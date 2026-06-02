namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Central catalog of the benchmark functions, so unit validation and the convergence matrix
/// refer to them by a single stable name. Functions defined only in two dimensions ignore the
/// requested dimension.
/// </summary>
public static class BenchmarkFunctionCatalog
{
    /// <summary>Creates a benchmark evaluator by name at the requested dimension.</summary>
    public static BenchmarkFunctionEvaluator Create(
        string name,
        int dimension) => name switch
    {
        "Sphere" => new SphereEvaluator(dimension),
        "Rosenbrock" => new RosenbrockEvaluator(dimension),
        "Zakharov" => new ZakharovEvaluator(dimension),
        "SumOfDifferentPowers" => new SumOfDifferentPowersEvaluator(dimension),
        "DixonPrice" => new DixonPriceEvaluator(dimension),
        "Rastrigin" => new RastriginEvaluator(dimension),
        "Ackley" => new AckleyEvaluator(dimension),
        "Griewank" => new GriewankEvaluator(dimension),
        "Levy" => new LevyEvaluator(dimension),
        "StyblinskiTang" => new StyblinskiTangEvaluator(dimension),
        "Schwefel" => new SchwefelEvaluator(dimension),
        "Booth" => new BoothEvaluator(),
        "Beale" => new BealeEvaluator(),
        "Himmelblau" => new HimmelblauEvaluator(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), $"Unknown benchmark function '{name}'."),
    };

    /// <summary>
    /// Functions that expose a single closed-form minimizer, so a unit test can verify that
    /// evaluating the function at its minimizer reproduces the declared global minimum value.
    /// </summary>
    public static IEnumerable<BenchmarkFunctionEvaluator> WithKnownMinimizer(
        int dimension) =>
    [
        new SphereEvaluator(dimension),
        new RosenbrockEvaluator(dimension),
        new ZakharovEvaluator(dimension),
        new SumOfDifferentPowersEvaluator(dimension),
        new RastriginEvaluator(dimension),
        new AckleyEvaluator(dimension),
        new GriewankEvaluator(dimension),
        new LevyEvaluator(dimension),
        new StyblinskiTangEvaluator(dimension),
        new BoothEvaluator(),
        new BealeEvaluator(),
    ];
}
