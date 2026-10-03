# API.md — Tests.Shared/FitnessFunctionEvaluators/Interfaces

Namespace: `DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces`.
An objective that also declares its domain and its known optimum.

## Contract ✅

```csharp
public interface ITestFitnessFunctionEvaluator : IFitnessFunctionEvaluator
{
    ReadOnlyMemory<double> GetLowerBounds();
    ReadOnlyMemory<double> GetUpperBounds();
    double GetGlobalMinimumFfValue();
    ReadOnlyMemory<double> GetGlobalMinimumGenes();
}
```

Bounds are equal in length; the minimum is the objective's value at its global
minimizer, minimization assumed. `GetGlobalMinimumGenes` may throw
`NotSupportedException` for a function without a single closed-form minimizer.
