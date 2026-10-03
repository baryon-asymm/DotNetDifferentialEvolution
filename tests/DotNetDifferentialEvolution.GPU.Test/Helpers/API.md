# API.md — GPU test helpers

Namespace: `DotNetDifferentialEvolution.GPU.Test.Helpers`. Builders of device
populations for tests. Everything not listed here is internal structure and may change.

## Population builders ✅

```csharp
public static class PopulationHelper
{
    public static DevicePopulation GetRandomPopulation(
        Accelerator device,
        int populationSize,
        int individualVectorSize,
        double maxFitnessFunctionValue = 100,
        double maxIndividualVectorValue = 1000);

    public static DevicePopulation GetPopulation(
        Accelerator device,
        int populationSize,
        int individualVectorSize,
        double fitnessFunctionValue = 0,
        double individualVectorValue = 0);
}
```

Each allocates a fitness buffer and a `DenseX` gene buffer on `device` and returns a
`DevicePopulation` view over them: random values in `[0, max)` from `Random.Shared`, or
one constant everywhere.

## Errors

None raised by the helpers; ILGPU's own allocation errors propagate.

## Side effects

Allocates two device buffers per call and returns only views; the `MemoryBuffer`
objects are dropped undisposed. Whether disposing the accelerator frees them was not
verified.

## Out of scope

- Disposal: there is no handle to dispose.
