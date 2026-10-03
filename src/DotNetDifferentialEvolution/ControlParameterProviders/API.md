# API.md — ControlParameterProviders

Namespace: `DotNetDifferentialEvolution.ControlParameterProviders`. The source of each
individual's mutation factor F and crossover probability CR. Everything not listed here
is internal structure and may change.

## Contract ✅

```csharp
public interface IControlParameterProvider
{
    void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability);
}
```

Called by the engine once per trial, on the worker that builds it, with that worker's
random provider; returns the F and CR for that trial. A configuration without a
provider gets `NaN` for both. Self-adaptive variants implement the contract with
per-individual state; the two below are stateless.

## Constant parameters ✅

```csharp
public class ConstantControlParameterProvider : IControlParameterProvider
{
    public ConstantControlParameterProvider(double mutationForce, double crossoverProbability);
    public void GetControlParameters(int individualIndex, BaseRandomProvider randomProvider,
        out double mutationForce, out double crossoverProbability);
}
```

The same F and CR for every individual; classic DE. Draws nothing.

## Dithered F ✅

```csharp
public class DitheredControlParameterProvider : IControlParameterProvider
{
    public DitheredControlParameterProvider(double minMutationForce, double maxMutationForce,
        double crossoverProbability);
    public void GetControlParameters(int individualIndex, BaseRandomProvider randomProvider,
        out double mutationForce, out double crossoverProbability);
}
```

F = `min + NextDouble() * (max - min)`, so in `[min, max)`; one uniform per call. CR is
constant.

## Errors

| Situation | Behaviour |
|---|---|
| Dithered: `min > max` | `ArgumentException`, in the constructor |
| Dithered: `null` provider | `ArgumentNullException` |
| Constant: `null` provider | Accepted; it is not used |
| F or CR outside their usual ranges, or `NaN` | Not checked in either class |

## Side effects

Dithered advances the provider by one uniform per call; constant touches nothing.

## Out of scope

- Adapting F and CR between generations: the generation strategies of the adaptive
  variants do that.
