# BOOT.md — GPU.Test/Objectives

## Purpose

ACCEPTANCE.md check **2a**: `GeneView`, all an objective receives, exposes no writable
member. With check 2b ([Kernels](../Kernels/API.md)) it holds the package's
race-freedom invariant (package `BOOT.md`, invariant 2): the objective cannot write,
and the kernel writes only slot i.

## Invariants

- **Reflection over every member, not a list of names.** All properties, of any
  accessibility, have no setter and no `init`; no method or property returns by `ref`;
  no field is public, instance or static. A member added later is covered without a
  test change.
- **The indexer is present** (asserted), so an empty reflection result cannot pass by
  accident.

## Dependencies

- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md) —
  `GeneView`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). Host-only, `Category=Unit`.

## Acceptance criteria

- [x] Green: 2026-10-03, `GeneViewSurfaceTests` 3 of 3.
- [x] Red: 2026-10-03, scratch mutation adding `set => _genes[index] = value;` to the
      indexer: `NoPropertyHasASetter` fails (`GeneViewSurfaceTests.cs` line 26,
      `set_Item(Int32, Double)`).

## Taboos

- **No allow-list of member names**: the check is about kinds of members.
