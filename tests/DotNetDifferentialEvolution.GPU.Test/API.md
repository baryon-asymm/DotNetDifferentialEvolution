# API.md — DotNetDifferentialEvolution.GPU.Test

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the GPU package may consider proven.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| A default run (10 000 individuals, 1 000 generations, F 0.3, CR 0.8) finds the minimum of 2-D Rosenbrock to ±1e-6 in fitness and in each coordinate | L2, `TestRosenbrockCase`, against the analytic minimum | ✅ local, OpenCL |
| The same run finds the least-squares coefficients of a degree-5 polynomial over 12 points to ±1e-8 | L2, `TestPolynomialApproximationFunctionCase`, against the exact solution | ✅ local, OpenCL |
| Each strategy is correct on its own | L0 | absent |
| The controller's lifecycle and cancellation behave as documented | L1 | absent |

Claims do not scale up the ladder: a green upper level with a red lower one means not
"the node is correct" but "matched for an unknown reason".

## What the tests rely on

- `GetOptimizer<T>(lowerValue, upperValue, individualSize, fitnessFunction)` in
  `DifferentialEvolutionOptimizerTests.cs`: builds the whole run with the defaults
  above, on the preferred OpenCL device. It returns the optimizer, which disposes the
  context and the device; the bound and random-state buffers are not disposed.
- The objectives and their expected answers: [FitnessFunctions](FitnessFunctions/API.md).
- [Helpers](Helpers/API.md): population builders, unused by any test.

## Test ✅

```csharp
[Trait("Category", "Gpu")]
public class DifferentialEvolutionOptimizerTests
{
    public DifferentialEvolutionOptimizerTests(ITestOutputHelper output);
    public async Task TestRosenbrockCase();
    public async Task TestPolynomialApproximationFunctionCase();
}
```
