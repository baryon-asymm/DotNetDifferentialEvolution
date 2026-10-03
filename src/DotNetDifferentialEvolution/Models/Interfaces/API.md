# API.md — Models/Interfaces

Namespace: `DotNetDifferentialEvolution.Models.Interfaces`. The visitor pair through
which a population points its cursor at an individual without copying. Everything not
listed here is internal structure and may change.

## Cursor and updater ✅

```csharp
public interface IIndividualCursor
{
    void AcceptUpdater(int individualIndex, IIndividualCursorUpdater updater);
}

public interface IIndividualCursorUpdater
{
    void Update(int individualIndex, ref double fitnessFunctionValue,
        ref ReadOnlyMemory<double> genes);
}
```

A cursor hands its two fields by reference to an updater, which overwrites them with
individual `individualIndex`'s fitness and a slice of its genes. No genes are copied.

## Errors

Implementation-defined.

## Side effects

The updater writes the cursor's fields.

## Out of scope

- Snapshots and copies: [Models](../API.md) (`IndividualCursor.GetSnapshot`).
