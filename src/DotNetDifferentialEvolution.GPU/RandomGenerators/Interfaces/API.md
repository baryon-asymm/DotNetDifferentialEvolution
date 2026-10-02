# API.md — GPU random generator contract

Namespace: `DotNetDifferentialEvolution.GPU.RandomGenerators.Interfaces`. The contract
a kernel uses to draw random numbers, one independent stream per index. Everything not
listed here is internal structure and may change.

## Contract ✅

```csharp
public interface IRandomGenerator
{
    double NextDouble(int index);
    float NextFloat(int index);
    uint NextUInt(int index);
    int Next(int index);
}
```

Implemented by a **struct** (the mutation strategy and the controller constrain it to
`struct, IRandomGenerator`) and called inside kernels. `index` selects the stream; the
package passes the individual's index, so each GPU thread draws from its own stream.
The interface says nothing about ranges; the package's implementation
([RandomGenerators](../API.md)) documents its own.

## Errors

| Situation | Behaviour |
|---|---|
| `index` outside the implementation's streams | Implementation-defined; the package's one reads outside its buffer |

## Side effects

A draw advances the stream it reads: implementations keep their state in device memory.

## Out of scope

- Seeding and the number of streams: the implementation's constructor owns them.
