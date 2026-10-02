# API.md — UnitTests/FitnessFunctions

Nothing outward. What this node proves about the shared benchmark library (not about
the package).

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| The eleven functions with a minimizer evaluate to their declared minimum there, at 2 and 5 dimensions | `EvaluatingAtTheKnownMinimizerReproducesTheGlobalMinimum` | ✅ (see the BOOT ⚠ on Styblinski-Tang) |
| All fourteen declare a proper box of the right length, at 2 and 4 dimensions | `DeclaresWellFormedBounds` | ✅ |
| The worker overload equals the plain one | `WorkerIndexedEvaluateMatchesPlainEvaluate` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class BenchmarkFunctionEvaluatorTests
{
    public void EvaluatingAtTheKnownMinimizerReproducesTheGlobalMinimum(int dimension);
    public void DeclaresWellFormedBounds(int dimension);
    public void WorkerIndexedEvaluateMatchesPlainEvaluate();
}
```
