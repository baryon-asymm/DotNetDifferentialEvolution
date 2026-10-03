# API.md — Tests.Common/FitnessFunctionEvaluators

Namespace: `DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators`. Benchmark
objectives with declared domains and optima, a catalog over them, and two objectives that
fail on purpose. Contract: [ITestFitnessFunctionEvaluator](Interfaces/API.md).

## Benchmark base ✅

```csharp
public abstract class BenchmarkFunctionEvaluator : ITestFitnessFunctionEvaluator
{
    protected BenchmarkFunctionEvaluator(int dimension);
    public virtual string Name { get; }                 // type name without "Evaluator"
    public int Dimension { get; }
    protected virtual int MinimumDimension { get; }     // 1
    public abstract double Evaluate(ReadOnlySpan<double> genes);
    public double Evaluate(int workerIndex, ReadOnlySpan<double> genes);  // = Evaluate(genes)
    public virtual ReadOnlyMemory<double> GetGlobalMinimumGenes();        // default: NotSupportedException
    protected ReadOnlyMemory<double> UniformBounds(double value);
    protected ReadOnlyMemory<double> UniformMinimizer(double value);
}
```

## Benchmark functions ✅

All minimized; the dimension defaults to 2.

| Type | Domain | Declared f* | Minimizer exposed |
|---|---|---|---|
| `SphereEvaluator` | [-5.12, 5.12]ⁿ | 0 | origin |
| `RosenbrockEvaluator` (n ≥ 2; `A`, `B` constants) | [-5, 5]ⁿ | 0 | (1, …, 1) |
| `ZakharovEvaluator` | [-5, 10]ⁿ | 0 | origin |
| `SumOfDifferentPowersEvaluator` | [-1, 1]ⁿ | 0 | origin |
| `DixonPriceEvaluator` | [-10, 10]ⁿ | 0 | no |
| `RastriginEvaluator` | [-5.12, 5.12]ⁿ | 0 | origin |
| `AckleyEvaluator` | [-32.768, 32.768]ⁿ | 0 | origin |
| `GriewankEvaluator` | [-600, 600]ⁿ | 0 | origin |
| `LevyEvaluator` | [-10, 10]ⁿ | 0 | (1, …, 1) |
| `StyblinskiTangEvaluator` (`Minimizer`, `MinimumValuePerDimension`) | [-5, 5]ⁿ | −39.16599·n | (−2.903534, …) |
| `SchwefelEvaluator` | [-500, 500]ⁿ | 0 | no |
| `BoothEvaluator()` (2-D) | [-10, 10]² | 0 | (1, 3) |
| `BealeEvaluator()` (2-D) | [-4.5, 4.5]² | 0 | (3, 0.5) |
| `HimmelblauEvaluator()` (2-D, four minima) | [-5, 5]² | 0 | no |

## Catalog ✅

```csharp
public static class BenchmarkFunctionCatalog
{
    public static BenchmarkFunctionEvaluator Create(string name, int dimension);
    public static IEnumerable<BenchmarkFunctionEvaluator> WithKnownMinimizer(int dimension);
}
```

`Create` takes the name without "Evaluator"; the 2-D functions ignore `dimension`.
`WithKnownMinimizer` lists the eleven functions that expose a minimizer.

## Failing objectives ✅

```csharp
public sealed class NaNSphereEvaluator : BenchmarkFunctionEvaluator
{
    public NaNSphereEvaluator(int firstNaNEvaluation, int lastNaNEvaluation = int.MaxValue, int dimension = 2);
    public int FirstNaNEvaluation { get; }
    public int LastNaNEvaluation { get; }
}

public class ExceptionRosenbrockEvaluator : RosenbrockEvaluator
{
    public ExceptionRosenbrockEvaluator(int throwExceptionAt, int dimension = 2);
    public int ThrowExceptionAt { get; init; }
}
public class RosenbrockException : Exception { /* three standard constructors */ }

public class SimpleSumEvaluator : ITestFitnessFunctionEvaluator
{
    public SimpleSumEvaluator(ReadOnlyMemory<double> lowerBounds, ReadOnlyMemory<double> upperBounds);
}
```

Evaluations are counted 1-based, atomically. `NaNSphereEvaluator` returns `NaN` for the
evaluations numbered within `[first, last]`, Sphere otherwise.
`ExceptionRosenbrockEvaluator` throws `RosenbrockException` on evaluation number
`ThrowExceptionAt` and every one after. `SimpleSumEvaluator` is `Σ xᵢ`, minimized at the
lower bound.

## Errors

| Situation | Behaviour |
|---|---|
| `dimension` below the function's minimum | `ArgumentOutOfRangeException` |
| `Create` with an unknown name | `ArgumentOutOfRangeException` |
| `GetGlobalMinimumGenes` on DixonPrice, Schwefel, Himmelblau | `NotSupportedException` |
| `SimpleSumEvaluator` bounds of different lengths | `ArgumentException` |
