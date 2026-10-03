# API.md — GPU test objectives

Namespace: `DotNetDifferentialEvolution.GPU.Test.FitnessFunctions`. Two objectives with
known optima, written as kernel structs, each carrying its own expected answer. Used
only by the tests of the parent node. Everything not listed here is internal structure
and may change.

## Objectives ✅

```csharp
public struct RosenbrockFunction : IFitnessFunctionInvoker
{
    public void Invoke(int individualIndex, DevicePopulation devicePopulation);
    public static double ExpectedFitnessValue { get; }        // 0
    public static IEnumerable<double> ExpectedIndividual { get; }  // [1, 1]
    public static int IndividualSize { get; }                 // 2
}

public readonly struct PolynomialApproximationFunction : IFitnessFunctionInvoker
{
    public void Invoke(int individualIndex, DevicePopulation devicePopulation);
    public static double ExpectedFitnessValue { get; }        // 2.3295763060466132E-05
    public static IEnumerable<double> ExpectedIndividual { get; }  // 6 coefficients
    public static int IndividualSize { get; }                 // 6
}
```

- `RosenbrockFunction`: `(1 - x)^2 + 100 (y - x^2)^2` over genes `x, y`; minimum 0 at
  `(1, 1)`.
- `PolynomialApproximationFunction`: the sum of squared residuals of the polynomial
  `c0 + c1 t + … + c5 t^5` against 12 points `(t, f)`, `t` from 1 to 6.5 in steps of
  0.5, written into the struct. The expected coefficients and fitness are the exact
  least-squares solution (see `BOOT.md`).

## Errors

None raised; kernel code.

## Side effects

Each `Invoke` writes `FitnessFunctionValues[individualIndex]` of the population it gets.

## Out of scope

- Any objective without a known optimum.
