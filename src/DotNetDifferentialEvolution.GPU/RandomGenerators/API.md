# API.md — GPU RandomGenerators

Namespace: `DotNetDifferentialEvolution.GPU.RandomGenerators`. The package's random
generator for kernels: one ILGPU `XorShift32` state per index, kept in device memory.
Everything not listed here is internal structure and may change.

## Generator ✅

```csharp
public struct RandomGenerator : IRandomGenerator
{
    public RandomGenerator(ArrayView<XorShift32> xorShifts);

    public double NextDouble(int index);
    public float NextFloat(int index);
    public uint NextUInt(int index);
    public int Next(int index);
}
```

Implements [the contract](Interfaces/API.md). The caller allocates and seeds the
states, one per individual, and passes the device view. Each call reads state
`xorShifts[index]`, draws one value and writes back `NextProvider()` of the advanced
state; the result is that a call takes every other value of that state's `XorShift32`
sequence (measured 2026-10-02 on the host, seed 42, 5 draws against 10 raw values).

Ranges, measured 2026-10-02 on the host with ILGPU.Algorithms 1.5.1, 2 000 000 draws
each: `Next` returned 0 negatives (min 265, max 2 147 483 243); `NextDouble` stayed
inside (0, 1) (min 3.96e-7, max 0.99999943).

How a caller sets it up (the pattern of the package's tests and README):

```csharp
var states = new XorShift32[populationSize];
for (var i = 0; i < states.Length; i++)
    states[i] = new XorShift32((uint)random.Next());   // must not be 0, see Errors
var generator = new RandomGenerator(device.Allocate1D(states).View);
```

## Errors

| Situation | Behaviour |
|---|---|
| A state constructed with seed 0 | ILGPU's `XorShift32` constructor asserts "State must not be zero" and the process terminates (measured on the host, 2026-10-02); this happens in the caller's code, before the generator exists |
| Fewer states than individuals | Not detected; a kernel thread reads and writes outside the buffer |

## Side effects

Every draw writes the stream's new state back into the caller's device buffer.

## Out of scope

- Allocating, seeding and freeing the state buffer: the caller does all three.
- Reproducibility: seeding is the caller's; the package offers no seed parameter.
