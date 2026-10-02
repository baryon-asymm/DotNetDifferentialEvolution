# API.md — Benchmark/Functions

Namespace: `DotNetDifferentialEvolution.Benchmark.Functions`. Two objectives used by
the convergence comparison.

## Objectives ✅

```csharp
public class RastriginEvaluator : IFitnessFunctionEvaluator
{
    public double Evaluate(ReadOnlySpan<double> genes);                  // 10n + Σ (x² − 10 cos 2πx)
    public double Evaluate(int workerIndex, ReadOnlySpan<double> genes); // = Evaluate(genes)
}

public class AckleyEvaluator : IFitnessFunctionEvaluator
{
    public double Evaluate(ReadOnlySpan<double> genes);  // −20 e^(−0.2 √(Σx²/n)) − e^(Σcos 2πx / n) + 20 + e
    public double Evaluate(int workerIndex, ReadOnlySpan<double> genes);
}
```

Both are minimized, with minimum 0 at the origin, and carry no bounds: the caller
supplies them (`ConvergenceComparison` uses ±5.12 and ±32.768).
